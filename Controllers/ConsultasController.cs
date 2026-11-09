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
/// Datos para las pantallas de resumen y consulta de la app (los mismos datos salen en PDF desde Reportes).
/// Los números que muestran costos o deudas con proveedores solo se calculan para el Admin.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Roles.Staff)]
public class ConsultasController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ReporteService _reportes;

    public ConsultasController(AppDbContext context, ReporteService reportes)
    {
        _context = context;
        _reportes = reportes;
    }

    /// <summary>Resumen de la pantalla de inicio: ventas y cobros de hoy, del mes, deudas y stock bajo.</summary>
    [HttpGet("resumen")]
    public async Task<ActionResult<DashboardDto>> Resumen()
    {
        var hoy = Reloj.Hoy;
        var manana = hoy.AddDays(1);
        var inicioMes = new DateTime(hoy.Year, hoy.Month, 1);
        var finMes = inicioMes.AddMonths(1);

        var dto = new DashboardDto();

        // Ventas de hoy (sin anuladas)
        var ventasHoy = _context.Ventas.AsNoTracking()
            .Where(v => v.Estado != EstadoVenta.Anulada && v.Fecha >= hoy && v.Fecha < manana);
        dto.VentasHoyCantidad = await ventasHoy.CountAsync();
        dto.VentasHoyTotal = await ventasHoy.SumAsync(v => v.Total);

        // Plata que efectivamente entró hoy, por medio de pago
        dto.CobradoHoyPorMedio = await _context.Cobros.AsNoTracking()
            .Where(c => c.Venta!.Estado != EstadoVenta.Anulada && c.Fecha >= hoy && c.Fecha < manana)
            .GroupBy(c => c.MedioPago!.Nombre)
            .Select(g => new CobradoPorMedioDto { MedioPago = g.Key, Monto = g.Sum(c => c.Monto) })
            .OrderByDescending(x => x.Monto)
            .ToListAsync();
        dto.CobradoHoy = dto.CobradoHoyPorMedio.Sum(x => x.Monto);

        // Mes en curso
        var ventasMes = _context.Ventas.AsNoTracking()
            .Where(v => v.Estado != EstadoVenta.Anulada && v.Fecha >= inicioMes && v.Fecha < finMes);
        dto.VentasMesCantidad = await ventasMes.CountAsync();
        dto.VentasMesTotal = await ventasMes.SumAsync(v => v.Total);

        // Lo que deben los clientes
        var pendientes = _context.Ventas.AsNoTracking().Where(v => v.Estado == EstadoVenta.Pendiente);
        dto.DeudaClientes = await pendientes.SumAsync(v => v.Total - v.MontoCobrado);
        dto.ClientesConDeuda = await pendientes.Where(v => v.IdCliente != null).Select(v => v.IdCliente).Distinct().CountAsync();

        // Stock
        dto.ProductosActivos = await _context.Productos.CountAsync(p => p.Activo);
        dto.ProductosStockBajo = await _context.Productos.CountAsync(p => p.Activo && p.Stock <= p.StockMinimo);

        dto.MasVendidosMes = await _reportes.MasVendidosAsync(inicioMes, finMes.AddDays(-1), 5);

        // Solo Admin: costos y deudas con proveedores
        if (User.EsAdmin())
        {
            var margenBruto = await _context.DetallesVenta.AsNoTracking()
                .Where(d => d.Venta!.Estado != EstadoVenta.Anulada && d.Venta.Fecha >= inicioMes && d.Venta.Fecha < finMes)
                .SumAsync(d => (d.PrecioUnitario - d.CostoUnitario) * d.Cantidad);
            var descuentos = await ventasMes.SumAsync(v => v.Descuento);
            dto.GananciaMes = margenBruto - descuentos;

            dto.DeudaProveedores = await _context.Ingresos.SumAsync(i => i.Total - i.MontoPagado);
            dto.ValorStockCosto = await _context.Productos.Where(p => p.Activo).SumAsync(p => p.Stock * p.PrecioCompra);
        }

        return Ok(dto);
    }

    /// <summary>Productos más vendidos del período (sin fechas = mes actual).</summary>
    [HttpGet("mas-vendidos")]
    public async Task<ActionResult<List<MasVendidoDto>>> MasVendidos(DateTime? desde = null, DateTime? hasta = null, int top = 10)
    {
        var (d, h) = ReporteService.ResolverRango(desde, hasta);
        if (d > h) return BadRequest("La fecha 'desde' no puede ser posterior a la fecha 'hasta'.");
        return Ok(await _reportes.MasVendidosAsync(d, h, Math.Clamp(top, 1, 100)));
    }

    /// <summary>Clientes con saldo pendiente, de mayor a menor deuda.</summary>
    [HttpGet("deudores")]
    public async Task<ActionResult<List<DeudorDto>>> Deudores() =>
        Ok(await _reportes.DeudoresAsync());

    /// <summary>Totales cobrados por medio de pago en un período (cierre de caja). Sin fechas = hoy.</summary>
    [HttpGet("caja")]
    public async Task<ActionResult<List<CobradoPorMedioDto>>> Caja(DateTime? desde = null, DateTime? hasta = null)
    {
        var d = (desde ?? hasta ?? Reloj.Hoy).Date;
        var h = (hasta ?? desde ?? Reloj.Hoy).Date.AddDays(1);
        if (d >= h) return BadRequest("La fecha 'desde' no puede ser posterior a la fecha 'hasta'.");

        return Ok(await _context.Cobros.AsNoTracking()
            .Where(c => c.Venta!.Estado != EstadoVenta.Anulada && c.Fecha >= d && c.Fecha < h)
            .GroupBy(c => c.MedioPago!.Nombre)
            .Select(g => new CobradoPorMedioDto { MedioPago = g.Key, Monto = g.Sum(c => c.Monto) })
            .OrderByDescending(x => x.Monto)
            .ToListAsync());
    }
}
