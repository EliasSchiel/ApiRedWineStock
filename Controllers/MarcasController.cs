using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RedWineStock.Common;
using RedWineStock.Data;
using RedWineStock.Dtos;
using RedWineStock.Models;

namespace RedWineStock.Controllers;

/// <summary>Marcas / bodegas de los productos.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Roles.Staff)]
public class MarcasController : ControllerBase
{
    private readonly AppDbContext _context;
    public MarcasController(AppDbContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<PagedResult<MarcaDto>>> GetAll(int page = 1, int pageSize = 100, bool incluirInactivas = false, string? busqueda = null)
    {
        var query = _context.Marcas.AsNoTracking().AsQueryable();
        if (!incluirInactivas) query = query.Where(m => m.Activo);
        if (!string.IsNullOrWhiteSpace(busqueda)) query = query.Where(m => m.Nombre.Contains(busqueda));

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(m => m.Nombre)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new MarcaDto { IdMarca = m.IdMarca, Nombre = m.Nombre, Activo = m.Activo })
            .ToListAsync();

        return Ok(new PagedResult<MarcaDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total });
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<MarcaDto>> GetById(int id)
    {
        var m = await _context.Marcas.AsNoTracking().FirstOrDefaultAsync(x => x.IdMarca == id);
        if (m is null) return NotFound();
        return Ok(new MarcaDto { IdMarca = m.IdMarca, Nombre = m.Nombre, Activo = m.Activo });
    }

    [HttpPost]
    public async Task<ActionResult<MarcaDto>> Create(MarcaCreateDto dto)
    {
        var nombre = dto.Nombre.Trim();
        if (await _context.Marcas.AnyAsync(m => m.Nombre == nombre))
            return BadRequest("Ya existe una marca con ese nombre.");

        var marca = new Marca { Nombre = nombre, Activo = dto.Activo };
        _context.Marcas.Add(marca);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = marca.IdMarca },
            new MarcaDto { IdMarca = marca.IdMarca, Nombre = marca.Nombre, Activo = marca.Activo });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, MarcaCreateDto dto)
    {
        var marca = await _context.Marcas.FindAsync(id);
        if (marca is null) return NotFound();

        var nombre = dto.Nombre.Trim();
        if (await _context.Marcas.AnyAsync(m => m.Nombre == nombre && m.IdMarca != id))
            return BadRequest("Ya existe una marca con ese nombre.");

        marca.Nombre = nombre;
        marca.Activo = dto.Activo;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(int id)
    {
        var marca = await _context.Marcas.FindAsync(id);
        if (marca is null) return NotFound();

        if (await _context.Productos.AnyAsync(p => p.IdMarca == id))
            return Conflict("No se puede eliminar: la marca tiene productos. Podés desactivarla.");

        _context.Marcas.Remove(marca);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
