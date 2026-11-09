using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RedWineStock.Common;
using RedWineStock.Data;
using RedWineStock.Dtos;
using RedWineStock.Models;
using RedWineStock.Services;

namespace RedWineStock.Controllers;

/// <summary>
/// Ventas del mostrador.
/// 1) POST api/ventas              -> registra la venta: descuenta stock, aplica ofertas vigentes, descuento final y cobros.
///                                    Si lo cobrado no alcanza el total queda "Pendiente" (exige cliente).
/// 2) POST api/ventas/{id}/cobros  -> registra un cobro posterior (cuando el cliente paga lo que debía).
/// 3) POST api/ventas/{id}/anular  -> solo Admin: devuelve el stock y marca la venta como Anulada.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Roles.Staff)]
public class VentasController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly StockService _stock;
    private readonly OfertaService _ofertas;
    private readonly ReporteService _reportes;

    public VentasController(AppDbContext context, StockService stock, OfertaService ofertas, ReporteService reportes)
    {
        _context = context;
        _stock = stock;
        _ofertas = ofertas;
        _reportes = reportes;
    }

    // ===================== CONSULTAS =====================

    /// <param name="soloConSaldo">true = solo ventas pendientes de cobro</param>
    [HttpGet]
    public async Task<ActionResult<PagedResult<VentaResumenDto>>> GetAll(
        int page = 1, int pageSize = 20, int? idCliente = null, EstadoVenta? estado = null,
        bool soloConSaldo = false, DateTime? desde = null, DateTime? hasta = null)
    {
        var query = _context.Ventas.AsNoTracking().AsQueryable();

        if (idCliente.HasValue) query = query.Where(v => v.IdCliente == idCliente);
        if (estado.HasValue) query = query.Where(v => v.Estado == estado);
        if (soloConSaldo) query = query.Where(v => v.Estado == EstadoVenta.Pendiente);
        if (desde.HasValue) query = query.Where(v => v.Fecha >= desde.Value.Date);
        if (hasta.HasValue) query = query.Where(v => v.Fecha < hasta.Value.Date.AddDays(1));

        var total = await query.CountAsync();
        var ventas = await query
            .Include(v => v.Cliente)
            .Include(v => v.Usuario)
            .Include(v => v.Detalles)
            .OrderByDescending(v => v.Fecha).ThenByDescending(v => v.IdVenta)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsSplitQuery()
            .ToListAsync();

        return Ok(new PagedResult<VentaResumenDto>
        {
            Items = ventas.Select(MapToResumen).ToList(), Page = page, PageSize = pageSize, TotalCount = total
        });
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<VentaDto>> GetById(int id)
    {
        var venta = await QueryCompleta().AsNoTracking().FirstOrDefaultAsync(v => v.IdVenta == id);
        if (venta is null) return NotFound();
        return Ok(MapToDto(venta));
    }

    /// <summary>Comprobante de la venta en PDF (no es una factura).</summary>
    [HttpGet("{id:int}/comprobante")]
    public async Task<IActionResult> Comprobante(int id)
    {
        var archivo = await _reportes.GenerarComprobanteAsync(id);
        if (archivo is null) return NotFound();
        return File(archivo.Contenido, "application/pdf", archivo.NombreArchivo);
    }

    // ===================== REGISTRAR VENTA =====================

    [HttpPost]
    public async Task<ActionResult<VentaDto>> Create(VentaCreateDto dto)
    {
        if (dto.IdCliente.HasValue &&
            !await _context.Clientes.AnyAsync(c => c.IdCliente == dto.IdCliente && c.Activo))
            return BadRequest(new { errores = new[] { "El cliente indicado no existe o está inactivo." } });

        // Si el mismo producto (o combo) viene repetido, se agrupa
        var items = dto.Items
            .GroupBy(i => i.IdProducto)
            .Select(g => new { IdProducto = g.Key, Cantidad = g.Sum(x => x.Cantidad) })
            .ToList();
        var combosPedidos = dto.Combos
            .GroupBy(c => c.IdCombo)
            .Select(g => new { IdCombo = g.Key, Cantidad = g.Sum(x => x.Cantidad) })
            .ToList();

        if (items.Count == 0 && combosPedidos.Count == 0)
            return BadRequest(new { errores = new[] { "La venta no tiene productos." } });

        var errorMedios = await ValidarMediosPago(dto.Cobros.Select(c => c.IdMedioPago));
        if (errorMedios is not null) return BadRequest(new { errores = new[] { errorMedios } });

        var idUsuario = User.IdUsuario();

        await using var tx = await _context.Database.BeginTransactionAsync();

        var errores = new List<string>();

        // Combos pedidos (con sus componentes)
        var idsCombos = combosPedidos.Select(c => c.IdCombo).ToList();
        var combos = await _context.Combos.Include(c => c.Items)
            .Where(c => idsCombos.Contains(c.IdCombo))
            .ToDictionaryAsync(c => c.IdCombo);
        foreach (var c in combosPedidos)
        {
            if (!combos.TryGetValue(c.IdCombo, out var combo) || !combo.Activo)
                errores.Add($"El combo {c.IdCombo} no existe o no está disponible.");
        }
        if (errores.Count > 0) return BadRequest(new { errores });

        // Cuántas unidades de cada producto necesita la venta en total (sueltas + dentro de combos)
        var necesarias = new Dictionary<int, int>();
        foreach (var item in items)
            necesarias[item.IdProducto] = necesarias.GetValueOrDefault(item.IdProducto) + item.Cantidad;
        foreach (var c in combosPedidos)
            foreach (var comp in combos[c.IdCombo].Items)
                necesarias[comp.IdProducto] = necesarias.GetValueOrDefault(comp.IdProducto) + comp.Cantidad * c.Cantidad;

        var ids = necesarias.Keys.ToList();
        var productos = await _context.Productos.Where(p => ids.Contains(p.IdProducto)).ToDictionaryAsync(p => p.IdProducto);

        foreach (var (idProducto, cantidad) in necesarias)
        {
            if (!productos.TryGetValue(idProducto, out var p) || !p.Activo)
                errores.Add($"El producto {idProducto} no existe o no está disponible.");
            else if (p.Stock < cantidad)
                errores.Add($"Stock insuficiente de '{p.Nombre}'. Disponible: {p.Stock}.");
        }
        if (errores.Count > 0) return BadRequest(new { errores });

        // Los precios los pone SIEMPRE el servidor (lista + oferta vigente hoy); la app nunca manda precios
        var ofertas = await _ofertas.VigentesAsync(items.Select(i => i.IdProducto));

        var venta = new Venta
        {
            Fecha = Reloj.Ahora,
            IdCliente = dto.IdCliente,
            IdUsuario = idUsuario,
            Observaciones = dto.Observaciones
        };

        foreach (var item in items)
        {
            var p = productos[item.IdProducto];
            ofertas.TryGetValue(p.IdProducto, out var oferta);
            var porcentaje = oferta?.PorcentajeDescuento ?? 0m;

            venta.Detalles.Add(new DetalleVenta
            {
                IdProducto = p.IdProducto,
                Cantidad = item.Cantidad,
                PrecioLista = p.PrecioVenta,
                PorcentajeOferta = porcentaje,
                IdOferta = oferta?.IdOferta,
                PrecioUnitario = OfertaService.AplicarDescuento(p.PrecioVenta, porcentaje),
                CostoUnitario = p.PrecioCompra
            });

            // Acá se descuenta el stock (y queda la fila en el historial de movimientos)
            _stock.Mover(p, TipoMovimiento.Venta, -item.Cantidad, idUsuario, venta: venta);
        }

        // Los combos: cada componente es un renglón con su parte del precio del combo (las ofertas no se aplican)
        foreach (var pedido in combosPedidos)
        {
            var combo = combos[pedido.IdCombo];
            foreach (var parte in RepartirCombo(combo, productos))
            {
                var p = productos[parte.IdProducto];
                var cantidad = parte.Cantidad * pedido.Cantidad;

                venta.Detalles.Add(new DetalleVenta
                {
                    IdProducto = p.IdProducto,
                    Cantidad = cantidad,
                    PrecioLista = p.PrecioVenta,
                    PorcentajeOferta = 0m,
                    IdCombo = combo.IdCombo,
                    PrecioUnitario = parte.PrecioUnitario,
                    CostoUnitario = p.PrecioCompra
                });

                _stock.Mover(p, TipoMovimiento.Venta, -cantidad, idUsuario, venta: venta);
            }
        }

        venta.Subtotal = venta.Detalles.Sum(d => d.Cantidad * d.PrecioUnitario);
        if (dto.Descuento > venta.Subtotal)
            return BadRequest(new { errores = new[] { $"El descuento ({dto.Descuento:N2}) no puede superar el subtotal ({venta.Subtotal:N2})." } });

        venta.Descuento = dto.Descuento;
        venta.Total = venta.Subtotal - venta.Descuento;

        var cobrado = dto.Cobros.Sum(c => c.Monto);
        if (cobrado > venta.Total)
            return BadRequest(new { errores = new[] { $"Lo cobrado ({cobrado:N2}) supera el total de la venta ({venta.Total:N2})." } });

        foreach (var c in dto.Cobros)
        {
            venta.Cobros.Add(new Cobro
            {
                IdMedioPago = c.IdMedioPago,
                Monto = c.Monto,
                Fecha = venta.Fecha,
                IdUsuario = idUsuario,
                Observaciones = c.Observaciones
            });
        }
        venta.MontoCobrado = cobrado;

        if (cobrado >= venta.Total)
        {
            venta.Estado = EstadoVenta.Cobrada;
        }
        else
        {
            if (dto.IdCliente is null)
                return BadRequest(new { errores = new[] { "Para dejar saldo pendiente tenés que indicar el cliente." } });
            venta.Estado = EstadoVenta.Pendiente;
        }

        _context.Ventas.Add(venta);
        await _context.SaveChangesAsync();
        await tx.CommitAsync();

        var creada = await QueryCompleta().AsNoTracking().FirstAsync(v => v.IdVenta == venta.IdVenta);
        return CreatedAtAction(nameof(GetById), new { id = venta.IdVenta }, MapToDto(creada));
    }

    // ===================== COBROS POSTERIORES =====================

    /// <summary>Registra un cobro de una venta pendiente. Si con esto se completa el total, pasa a "Cobrada".</summary>
    [HttpPost("{id:int}/cobros")]
    public async Task<ActionResult<VentaDto>> RegistrarCobro(int id, CobroCreateDto dto)
    {
        var venta = await _context.Ventas.FirstOrDefaultAsync(v => v.IdVenta == id);
        if (venta is null) return NotFound();

        if (venta.Estado == EstadoVenta.Anulada) return BadRequest("La venta está anulada, no se pueden registrar cobros.");
        if (venta.Estado == EstadoVenta.Cobrada) return BadRequest("La venta ya está cobrada por completo.");
        if (dto.Monto > venta.Saldo)
            return BadRequest($"El cobro ({dto.Monto:N2}) supera el saldo pendiente ({venta.Saldo:N2}).");

        var errorMedios = await ValidarMediosPago(new[] { dto.IdMedioPago });
        if (errorMedios is not null) return BadRequest(errorMedios);

        _context.Cobros.Add(new Cobro
        {
            IdVenta = id,
            IdMedioPago = dto.IdMedioPago,
            Monto = dto.Monto,
            Fecha = Reloj.Ahora,
            IdUsuario = User.IdUsuario(),
            Observaciones = dto.Observaciones
        });

        venta.MontoCobrado += dto.Monto;
        if (venta.MontoCobrado >= venta.Total) venta.Estado = EstadoVenta.Cobrada;

        await _context.SaveChangesAsync();

        var actualizada = await QueryCompleta().AsNoTracking().FirstAsync(v => v.IdVenta == id);
        return Ok(MapToDto(actualizada));
    }

    // ===================== ANULAR =====================

    /// <summary>
    /// Solo Admin. Devuelve el stock al inventario y marca la venta como Anulada.
    /// Se asume que lo ya cobrado se devuelve al cliente (las ventas anuladas no suman en los totales).
    /// </summary>
    [HttpPost("{id:int}/anular")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Anular(int id, AnularVentaDto dto)
    {
        var venta = await _context.Ventas
            .Include(v => v.Detalles).ThenInclude(d => d.Producto)
            .FirstOrDefaultAsync(v => v.IdVenta == id);
        if (venta is null) return NotFound();
        if (venta.Estado == EstadoVenta.Anulada) return BadRequest("La venta ya está anulada.");

        var idUsuario = User.IdUsuario();
        var motivo = dto.Motivo.Trim();

        await using var tx = await _context.Database.BeginTransactionAsync();

        foreach (var d in venta.Detalles)
            _stock.Mover(d.Producto!, TipoMovimiento.Anulacion, d.Cantidad, idUsuario, $"Anulación de venta N° {venta.IdVenta}: {motivo}", venta: venta);

        venta.Estado = EstadoVenta.Anulada;
        venta.FechaAnulacion = Reloj.Ahora;
        venta.MotivoAnulacion = motivo;
        venta.IdUsuarioAnulacion = idUsuario;

        await _context.SaveChangesAsync();
        await tx.CommitAsync();
        return NoContent();
    }

    // ===================== Helpers =====================

    private async Task<string?> ValidarMediosPago(IEnumerable<int> idsMedio)
    {
        var ids = idsMedio.Distinct().ToList();
        if (ids.Count == 0) return null;

        var validos = await _context.MediosPago.Where(m => ids.Contains(m.IdMedioPago) && m.Activo).CountAsync();
        return validos == ids.Count ? null : "Alguno de los medios de pago indicados no existe.";
    }

    private record ParteCombo(int IdProducto, int Cantidad, decimal PrecioUnitario);

    /// <summary>
    /// Reparte el precio del combo entre sus componentes en proporción a su precio de lista, de modo que
    /// UN combo sume exactamente su precio. Cada parte es (producto, unidades por combo, precio unitario).
    /// </summary>
    private static List<ParteCombo> RepartirCombo(Combo combo, Dictionary<int, Producto> productos)
    {
        var componentes = combo.Items.OrderBy(i => i.IdComboItem).ToList();

        // Peso de cada componente = lista x cantidad (si todo fuera gratis, se reparte por cantidad)
        decimal Peso(ComboItem i) => productos[i.IdProducto].PrecioVenta * i.Cantidad;
        var pesoTotal = componentes.Sum(i => Peso(i));
        var usarCantidad = pesoTotal <= 0;
        if (usarCantidad) pesoTotal = componentes.Sum(i => (decimal)i.Cantidad);

        var partes = new List<ParteCombo>();
        var acumulado = 0m;
        for (var n = 0; n < componentes.Count; n++)
        {
            var c = componentes[n];
            var peso = usarCantidad ? c.Cantidad : Peso(c);

            // El último componente se lleva el resto para que la suma dé exacto
            var totalComponente = n == componentes.Count - 1
                ? combo.Precio - acumulado
                : Math.Round(combo.Precio * peso / pesoTotal, 2);
            acumulado += totalComponente;

            partes.Add(new ParteCombo(c.IdProducto, c.Cantidad, Math.Round(totalComponente / c.Cantidad, 2)));
        }

        // Redondear precios unitarios puede dejar centavos de diferencia: se corrigen en un renglón de 1 unidad
        var diferencia = combo.Precio - partes.Sum(x => x.Cantidad * x.PrecioUnitario);
        if (diferencia != 0)
        {
            var idx = partes.FindLastIndex(x => x.Cantidad == 1);
            if (idx >= 0) partes[idx] = partes[idx] with { PrecioUnitario = partes[idx].PrecioUnitario + diferencia };
        }
        return partes;
    }

    private IQueryable<Venta> QueryCompleta() => _context.Ventas
        .Include(v => v.Cliente)
        .Include(v => v.Usuario)
        .Include(v => v.Detalles).ThenInclude(d => d.Producto).ThenInclude(p => p!.Marca)
        .Include(v => v.Detalles).ThenInclude(d => d.Combo)
        .Include(v => v.Cobros).ThenInclude(c => c.MedioPago)
        .Include(v => v.Cobros).ThenInclude(c => c.Usuario)
        .AsSplitQuery();

    private static VentaResumenDto MapToResumen(Venta v) => new()
    {
        IdVenta = v.IdVenta,
        Fecha = v.Fecha,
        IdCliente = v.IdCliente,
        ClienteNombre = v.Cliente?.Nombre,
        UsuarioNombre = v.Usuario?.Nombre,
        Total = v.Total,
        MontoCobrado = v.MontoCobrado,
        Saldo = v.Saldo,
        Estado = v.Estado.ToString(),
        CantidadItems = v.Detalles.Sum(d => d.Cantidad)
    };

    private static VentaDto MapToDto(Venta v) => new()
    {
        IdVenta = v.IdVenta,
        Fecha = v.Fecha,
        IdCliente = v.IdCliente,
        ClienteNombre = v.Cliente?.Nombre,
        UsuarioNombre = v.Usuario?.Nombre,
        Subtotal = v.Subtotal,
        Descuento = v.Descuento,
        Total = v.Total,
        MontoCobrado = v.MontoCobrado,
        Saldo = v.Saldo,
        Estado = v.Estado.ToString(),
        Observaciones = v.Observaciones,
        FechaAnulacion = v.FechaAnulacion,
        MotivoAnulacion = v.MotivoAnulacion,
        Detalles = v.Detalles.Select(d => new DetalleVentaDto
        {
            IdProducto = d.IdProducto,
            ProductoNombre = d.Producto?.Nombre,
            ProductoMarca = d.Producto?.Marca?.Nombre,
            ProductoCosecha = d.Producto?.Cosecha,
            IdCombo = d.IdCombo,
            ComboNombre = d.Combo?.Nombre,
            Cantidad = d.Cantidad,
            PrecioLista = d.PrecioLista,
            PorcentajeOferta = d.PorcentajeOferta,
            PrecioUnitario = d.PrecioUnitario
        }).ToList(),
        Cobros = v.Cobros.OrderBy(c => c.Fecha).Select(c => new CobroDto
        {
            IdCobro = c.IdCobro,
            Fecha = c.Fecha,
            IdMedioPago = c.IdMedioPago,
            MedioPago = c.MedioPago?.Nombre,
            Monto = c.Monto,
            UsuarioNombre = c.Usuario?.Nombre,
            Observaciones = c.Observaciones
        }).ToList()
    };
}
