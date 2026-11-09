using Microsoft.EntityFrameworkCore;
using RedWineStock.Common;
using RedWineStock.Data;
using RedWineStock.Models;

namespace RedWineStock.Services;

/// <summary>Busca qué ofertas están vigentes hoy para un conjunto de productos.</summary>
public class OfertaService
{
    private readonly AppDbContext _context;
    public OfertaService(AppDbContext context) => _context = context;

    /// <summary>Por producto, la oferta vigente de mayor descuento (si hay más de una).</summary>
    public async Task<Dictionary<int, Oferta>> VigentesAsync(IEnumerable<int> idsProducto)
    {
        var ids = idsProducto.Distinct().ToList();
        var hoy = Reloj.Hoy;

        var ofertas = await _context.Ofertas.AsNoTracking()
            .Where(o => ids.Contains(o.IdProducto) && o.Activa && o.FechaDesde <= hoy && o.FechaHasta >= hoy)
            .ToListAsync();

        return ofertas
            .GroupBy(o => o.IdProducto)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(o => o.PorcentajeDescuento).First());
    }

    public static decimal AplicarDescuento(decimal precio, decimal porcentaje) =>
        Math.Round(precio * (1 - porcentaje / 100m), 2);
}
