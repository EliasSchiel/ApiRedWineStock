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
/// Ajustes manuales de stock (con motivo obligatorio) e historial de movimientos.
/// El stock también cambia con Ventas (resta), Anulaciones (devuelve) e Ingresos (suma),
/// y todos esos cambios quedan en el mismo historial.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Roles.Staff)]
public class StockController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly StockService _stock;

    public StockController(AppDbContext context, StockService stock)
    {
        _context = context;
        _stock = stock;
    }

    /// <summary>Corrige el stock de un producto: se indica el stock real y el motivo (rotura, conteo, regalo...).</summary>
    [HttpPost("ajuste")]
    public async Task<ActionResult<MovimientoStockDto>> Ajustar(AjusteStockDto dto)
    {
        var producto = await _context.Productos.FirstOrDefaultAsync(p => p.IdProducto == dto.IdProducto);
        if (producto is null) return NotFound("El producto indicado no existe.");

        var diferencia = dto.NuevoStock - producto.Stock;
        if (diferencia == 0)
            return BadRequest($"El stock actual de '{producto.Nombre}' ya es {producto.Stock}; no hay nada que ajustar.");

        var movimiento = _stock.Mover(producto, TipoMovimiento.Ajuste, diferencia, User.IdUsuario(), dto.Motivo.Trim());
        await _context.SaveChangesAsync();

        var nombreUsuario = await _context.Usuarios.Where(u => u.IdUsuario == movimiento.IdUsuario)
            .Select(u => u.Nombre).FirstOrDefaultAsync();
        return Ok(MapToDto(movimiento, producto.Nombre, nombreUsuario));
    }

    /// <summary>Historial de movimientos de stock (entradas, ventas, anulaciones y ajustes).</summary>
    [HttpGet("movimientos")]
    public async Task<ActionResult<PagedResult<MovimientoStockDto>>> GetMovimientos(
        int page = 1, int pageSize = 30, int? idProducto = null, TipoMovimiento? tipo = null,
        DateTime? desde = null, DateTime? hasta = null)
    {
        var query = _context.MovimientosStock.AsNoTracking().AsQueryable();

        if (idProducto.HasValue) query = query.Where(m => m.IdProducto == idProducto);
        if (tipo.HasValue) query = query.Where(m => m.Tipo == tipo);
        if (desde.HasValue) query = query.Where(m => m.Fecha >= desde.Value.Date);
        if (hasta.HasValue) query = query.Where(m => m.Fecha < hasta.Value.Date.AddDays(1));

        var total = await query.CountAsync();
        var movimientos = await query
            .Include(m => m.Producto)
            .Include(m => m.Usuario)
            .OrderByDescending(m => m.Fecha).ThenByDescending(m => m.IdMovimientoStock)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new PagedResult<MovimientoStockDto>
        {
            Items = movimientos.Select(m => MapToDto(m, m.Producto?.Nombre, m.Usuario?.Nombre)).ToList(),
            Page = page, PageSize = pageSize, TotalCount = total
        });
    }

    private static MovimientoStockDto MapToDto(MovimientoStock m, string? producto, string? usuario) => new()
    {
        IdMovimientoStock = m.IdMovimientoStock,
        Fecha = m.Fecha,
        IdProducto = m.IdProducto,
        ProductoNombre = producto,
        Tipo = m.Tipo.ToString(),
        Cantidad = m.Cantidad,
        StockResultante = m.StockResultante,
        Motivo = m.Motivo,
        UsuarioNombre = usuario,
        IdVenta = m.IdVenta,
        IdIngreso = m.IdIngreso
    };
}
