using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RedWineStock.Common;
using RedWineStock.Data;
using RedWineStock.Dtos;
using RedWineStock.Models;

namespace RedWineStock.Controllers;

/// <summary>
/// Combos: varios productos con un precio total fijo. Se venden desde "Nueva venta".
/// Ver: Staff (el operador los necesita para vender). Crear / modificar / dar de baja: Admin.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Roles.Staff)]
public class CombosController : ControllerBase
{
    private readonly AppDbContext _context;
    public CombosController(AppDbContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<PagedResult<ComboDto>>> GetAll(int page = 1, int pageSize = 50, bool soloActivos = true)
    {
        var query = QueryCompleta().AsNoTracking();
        if (soloActivos) query = query.Where(c => c.Activo);

        var total = await query.CountAsync();
        var combos = await query
            .OrderBy(c => c.Nombre)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new PagedResult<ComboDto>
        {
            Items = combos.Select(MapToDto).ToList(), Page = page, PageSize = pageSize, TotalCount = total
        });
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ComboDto>> GetById(int id)
    {
        var combo = await QueryCompleta().AsNoTracking().FirstOrDefaultAsync(c => c.IdCombo == id);
        return combo is null ? NotFound() : Ok(MapToDto(combo));
    }

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<ComboDto>> Create(ComboCreateDto dto)
    {
        var items = Agrupar(dto.Items);
        var error = await Validar(dto, items);
        if (error is not null) return BadRequest(error);

        var combo = new Combo
        {
            Nombre = dto.Nombre.Trim(),
            Descripcion = string.IsNullOrWhiteSpace(dto.Descripcion) ? null : dto.Descripcion.Trim(),
            Precio = dto.Precio,
            Activo = true
        };
        foreach (var i in items)
            combo.Items.Add(new ComboItem { IdProducto = i.IdProducto, Cantidad = i.Cantidad });

        _context.Combos.Add(combo);
        await _context.SaveChangesAsync();

        var creado = await QueryCompleta().AsNoTracking().FirstAsync(c => c.IdCombo == combo.IdCombo);
        return CreatedAtAction(nameof(GetById), new { id = combo.IdCombo }, MapToDto(creado));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Update(int id, ComboUpdateDto dto)
    {
        var combo = await _context.Combos.Include(c => c.Items).FirstOrDefaultAsync(c => c.IdCombo == id);
        if (combo is null) return NotFound();

        var items = Agrupar(dto.Items);
        var error = await Validar(dto, items);
        if (error is not null) return BadRequest(error);

        combo.Nombre = dto.Nombre.Trim();
        combo.Descripcion = string.IsNullOrWhiteSpace(dto.Descripcion) ? null : dto.Descripcion.Trim();
        combo.Precio = dto.Precio;
        combo.Activo = dto.Activo;

        // Se sincronizan los componentes (las ventas ya hechas no se alteran: guardan sus propios renglones)
        var idsNuevos = items.Select(i => i.IdProducto).ToHashSet();
        foreach (var viejo in combo.Items.Where(x => !idsNuevos.Contains(x.IdProducto)).ToList())
            _context.ComboItems.Remove(viejo);

        foreach (var i in items)
        {
            var existente = combo.Items.FirstOrDefault(x => x.IdProducto == i.IdProducto);
            if (existente is null) combo.Items.Add(new ComboItem { IdProducto = i.IdProducto, Cantidad = i.Cantidad });
            else existente.Cantidad = i.Cantidad;
        }

        await _context.SaveChangesAsync();
        return NoContent();
    }

    // Baja lógica: las ventas ya hechas guardan una referencia al combo
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(int id)
    {
        var combo = await _context.Combos.FindAsync(id);
        if (combo is null) return NotFound();

        combo.Activo = false;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // ===================== Helpers =====================

    /// <summary>Si el mismo producto viene repetido, se suman las cantidades.</summary>
    private static List<ComboItemCreateDto> Agrupar(IEnumerable<ComboItemCreateDto> items) =>
        items.GroupBy(i => i.IdProducto)
             .Select(g => new ComboItemCreateDto { IdProducto = g.Key, Cantidad = g.Sum(x => x.Cantidad) })
             .ToList();

    private async Task<string?> Validar(ComboCreateDto dto, List<ComboItemCreateDto> items)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre)) return "El combo necesita un nombre.";
        if (items.Sum(i => i.Cantidad) < 2) return "Un combo tiene que tener al menos 2 unidades (por ejemplo 2 productos distintos, o 2 del mismo).";

        var ids = items.Select(i => i.IdProducto).ToList();
        var validos = await _context.Productos.CountAsync(p => ids.Contains(p.IdProducto) && p.Activo);
        return validos == ids.Count ? null : "Alguno de los productos indicados no existe o está inactivo.";
    }

    private IQueryable<Combo> QueryCompleta() => _context.Combos
        .Include(c => c.Items).ThenInclude(i => i.Producto).ThenInclude(p => p!.Marca);

    private static ComboDto MapToDto(Combo c)
    {
        var items = c.Items.OrderBy(i => i.IdComboItem).Select(i => new ComboItemDto
        {
            IdProducto = i.IdProducto,
            ProductoNombre = i.Producto?.Nombre,
            ProductoMarca = i.Producto?.Marca?.Nombre,
            ProductoCosecha = i.Producto?.Cosecha,
            ProductoStock = i.Producto?.Stock ?? 0,
            PrecioVenta = i.Producto?.PrecioVenta ?? 0,
            Cantidad = i.Cantidad
        }).ToList();

        var lista = items.Sum(i => i.PrecioVenta * i.Cantidad);
        return new ComboDto
        {
            IdCombo = c.IdCombo,
            Nombre = c.Nombre,
            Descripcion = c.Descripcion,
            Precio = c.Precio,
            PrecioLista = lista,
            Ahorro = lista - c.Precio,
            Activo = c.Activo,
            Disponible = items.Count == 0 ? 0 : items.Min(i => Math.Max(0, i.ProductoStock) / i.Cantidad),
            Items = items
        };
    }
}
