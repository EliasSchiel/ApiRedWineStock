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
/// Entregas de mercadería de proveedores. Suman stock (con historial), actualizan el último costo del
/// producto y, si no se pagó todo, quedan como deuda con el proveedor. Solo Admin (muestra costos).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Roles.Admin)]
public class IngresosController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly StockService _stock;
    private readonly ReporteService _reportes;

    public IngresosController(AppDbContext context, StockService stock, ReporteService reportes)
    {
        _context = context;
        _stock = stock;
        _reportes = reportes;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<IngresoDto>>> GetAll(
        int page = 1, int pageSize = 20, int? idProveedor = null, int? idProducto = null,
        bool soloConSaldo = false, DateTime? desde = null, DateTime? hasta = null, bool soloVencidas = false)
    {
        var query = QueryCompleta().AsNoTracking();

        if (idProveedor.HasValue) query = query.Where(i => i.IdProveedor == idProveedor);
        if (idProducto.HasValue) query = query.Where(i => i.Detalles.Any(d => d.IdProducto == idProducto));
        if (soloConSaldo) query = query.Where(i => i.Total > i.MontoPagado);
        if (soloVencidas)
        {
            var hoy = Reloj.Hoy;
            query = query.Where(i => i.Total > i.MontoPagado && i.FechaVencimiento != null && i.FechaVencimiento < hoy);
        }
        if (desde.HasValue) query = query.Where(i => i.Fecha >= desde.Value.Date);
        if (hasta.HasValue) query = query.Where(i => i.Fecha < hasta.Value.Date.AddDays(1));

        var total = await query.CountAsync();
        var ingresos = await query
            .OrderByDescending(i => i.Fecha)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new PagedResult<IngresoDto> { Items = ingresos.Select(MapToDto).ToList(), Page = page, PageSize = pageSize, TotalCount = total });
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<IngresoDto>> GetById(int id)
    {
        var ingreso = await QueryCompleta().AsNoTracking().FirstOrDefaultAsync(i => i.IdIngreso == id);
        if (ingreso is null) return NotFound();
        return Ok(MapToDto(ingreso));
    }

    /// <summary>Lo que se le debe a cada proveedor (resumen). Equivale al reporte "deudas-proveedores".</summary>
    [HttpGet("deudas")]
    public async Task<ActionResult<List<DeudaProveedorDto>>> GetDeudas() =>
        Ok(await _reportes.DeudasProveedoresAsync());

    [HttpPost]
    public async Task<ActionResult<IngresoDto>> Create(IngresoCreateDto dto)
    {
        if (!await _context.Proveedores.AnyAsync(p => p.IdProveedor == dto.IdProveedor && p.Activo))
            return BadRequest("El proveedor indicado no existe o está inactivo.");

        if (dto.Items.Any(i => i.Cajas + i.Cantidad <= 0))
            return BadRequest("Cada producto del ingreso necesita al menos una caja o una unidad.");

        if (dto.FechaVencimiento.HasValue && dto.FechaVencimiento.Value.Date < Reloj.Hoy)
            return BadRequest("La fecha de vencimiento no puede ser anterior a hoy.");

        var idUsuario = User.IdUsuario();
        var ids = dto.Items.Select(i => i.IdProducto).Distinct().ToList();

        await using var tx = await _context.Database.BeginTransactionAsync();

        var productos = await _context.Productos.Where(p => ids.Contains(p.IdProducto)).ToDictionaryAsync(p => p.IdProducto);
        var faltantes = ids.Where(id => !productos.ContainsKey(id)).ToList();
        if (faltantes.Count > 0)
            return BadRequest($"No existen los productos: {string.Join(", ", faltantes)}.");

        // Cada ítem llega en cajas y/o botellas sueltas: unidades = cajas × botellas por caja + sueltas.
        // Si el mismo producto viene repetido se agrupa (costo = promedio ponderado)
        var items = dto.Items
            .GroupBy(i => i.IdProducto)
            .Select(g =>
            {
                var porCaja = productos[g.Key].UnidadesPorCaja;
                var unidades = g.Sum(x => x.Cajas * porCaja + x.Cantidad);
                var costo = Math.Round(g.Sum(x => (x.Cajas * porCaja + x.Cantidad) * x.CostoUnitario) / unidades, 2);
                return new { IdProducto = g.Key, Cajas = g.Sum(x => x.Cajas), Cantidad = unidades, CostoUnitario = costo };
            })
            .ToList();

        var total = items.Sum(i => i.Cantidad * i.CostoUnitario);
        var pagado = dto.Pagos.Sum(p => p.Monto);
        if (pagado > total)
            return BadRequest($"Los pagos ({pagado:N2}) superan el total del ingreso ({total:N2}).");

        var errorPagos = await ValidarMediosPago(dto.Pagos.Select(p => p.IdMedioPago));
        if (errorPagos is not null) return BadRequest(errorPagos);

        var ingreso = new Ingreso
        {
            Fecha = Reloj.Ahora,
            IdProveedor = dto.IdProveedor,
            IdUsuario = idUsuario,
            NumeroComprobante = dto.NumeroComprobante,
            Observaciones = dto.Observaciones,
            Total = total,
            MontoPagado = pagado,
            FechaVencimiento = dto.FechaVencimiento?.Date
        };

        foreach (var item in items)
        {
            var producto = productos[item.IdProducto];
            ingreso.Detalles.Add(new DetalleIngreso
            {
                IdProducto = item.IdProducto,
                Cajas = item.Cajas,
                Cantidad = item.Cantidad,
                CostoUnitario = item.CostoUnitario
            });

            producto.PrecioCompra = item.CostoUnitario;   // el último costo pasa a ser el costo del producto
            _stock.Mover(producto, TipoMovimiento.Entrada, item.Cantidad, idUsuario,
                $"Ingreso de mercadería{(string.IsNullOrWhiteSpace(dto.NumeroComprobante) ? "" : " (comprobante " + dto.NumeroComprobante + ")")}",
                ingreso: ingreso);
        }

        foreach (var pago in dto.Pagos)
        {
            ingreso.Pagos.Add(new PagoProveedor
            {
                IdMedioPago = pago.IdMedioPago,
                Monto = pago.Monto,
                Fecha = Reloj.Ahora,
                IdUsuario = idUsuario,
                Observaciones = pago.Observaciones
            });
        }

        _context.Ingresos.Add(ingreso);
        await _context.SaveChangesAsync();
        await tx.CommitAsync();

        var creado = await QueryCompleta().AsNoTracking().FirstAsync(i => i.IdIngreso == ingreso.IdIngreso);
        return CreatedAtAction(nameof(GetById), new { id = ingreso.IdIngreso }, MapToDto(creado));
    }

    /// <summary>Cambia (o quita) la fecha de vencimiento de una entrega, por ejemplo si el proveedor extiende el plazo.</summary>
    [HttpPut("{id:int}/vencimiento")]
    public async Task<ActionResult<IngresoDto>> CambiarVencimiento(int id, IngresoVencimientoDto dto)
    {
        var ingreso = await _context.Ingresos.FirstOrDefaultAsync(i => i.IdIngreso == id);
        if (ingreso is null) return NotFound();

        ingreso.FechaVencimiento = dto.FechaVencimiento?.Date;
        await _context.SaveChangesAsync();

        var actualizado = await QueryCompleta().AsNoTracking().FirstAsync(i => i.IdIngreso == id);
        return Ok(MapToDto(actualizado));
    }

    /// <summary>Registra un pago al proveedor por una entrega (total o parcial).</summary>
    [HttpPost("{id:int}/pagos")]
    public async Task<ActionResult<IngresoDto>> RegistrarPago(int id, PagoProveedorCreateDto dto)
    {
        var ingreso = await _context.Ingresos.FirstOrDefaultAsync(i => i.IdIngreso == id);
        if (ingreso is null) return NotFound();

        if (ingreso.Saldo <= 0) return BadRequest("Este ingreso ya está pagado por completo.");
        if (dto.Monto > ingreso.Saldo)
            return BadRequest($"El pago ({dto.Monto:N2}) supera el saldo del ingreso ({ingreso.Saldo:N2}).");

        var errorPagos = await ValidarMediosPago(new[] { dto.IdMedioPago });
        if (errorPagos is not null) return BadRequest(errorPagos);

        _context.PagosProveedor.Add(new PagoProveedor
        {
            IdIngreso = id,
            IdMedioPago = dto.IdMedioPago,
            Monto = dto.Monto,
            Fecha = Reloj.Ahora,
            IdUsuario = User.IdUsuario(),
            Observaciones = dto.Observaciones
        });
        ingreso.MontoPagado += dto.Monto;

        await _context.SaveChangesAsync();

        var actualizado = await QueryCompleta().AsNoTracking().FirstAsync(i => i.IdIngreso == id);
        return Ok(MapToDto(actualizado));
    }

    private async Task<string?> ValidarMediosPago(IEnumerable<int> idsMedio)
    {
        var ids = idsMedio.Distinct().ToList();
        if (ids.Count == 0) return null;

        var validos = await _context.MediosPago.Where(m => ids.Contains(m.IdMedioPago) && m.Activo).CountAsync();
        return validos == ids.Count ? null : "Alguno de los medios de pago indicados no existe.";
    }

    private IQueryable<Ingreso> QueryCompleta() => _context.Ingresos
        .Include(i => i.Proveedor)
        .Include(i => i.Usuario)
        .Include(i => i.Detalles).ThenInclude(d => d.Producto).ThenInclude(p => p!.Marca)
        .Include(i => i.Pagos).ThenInclude(p => p.MedioPago)
        .Include(i => i.Pagos).ThenInclude(p => p.Usuario)
        .AsSplitQuery();

    private static IngresoDto MapToDto(Ingreso i) => new()
    {
        IdIngreso = i.IdIngreso,
        Fecha = i.Fecha,
        IdProveedor = i.IdProveedor,
        ProveedorNombre = i.Proveedor?.Nombre,
        UsuarioNombre = i.Usuario?.Nombre,
        NumeroComprobante = i.NumeroComprobante,
        Observaciones = i.Observaciones,
        Total = i.Total,
        MontoPagado = i.MontoPagado,
        Saldo = i.Saldo,
        FechaVencimiento = i.FechaVencimiento,
        Detalles = i.Detalles.Select(d => new DetalleIngresoDto
        {
            IdProducto = d.IdProducto,
            ProductoNombre = d.Producto?.Nombre,
            ProductoMarca = d.Producto?.Marca?.Nombre,
            ProductoCosecha = d.Producto?.Cosecha,
            Cajas = d.Cajas,
            Cantidad = d.Cantidad,
            CostoUnitario = d.CostoUnitario
        }).ToList(),
        Pagos = i.Pagos.OrderBy(p => p.Fecha).Select(p => new PagoProveedorDto
        {
            IdPagoProveedor = p.IdPagoProveedor,
            Fecha = p.Fecha,
            IdMedioPago = p.IdMedioPago,
            MedioPago = p.MedioPago?.Nombre,
            Monto = p.Monto,
            UsuarioNombre = p.Usuario?.Nombre,
            Observaciones = p.Observaciones
        }).ToList()
    };
}
