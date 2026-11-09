using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RedWineStock.Common;
using RedWineStock.Data;
using RedWineStock.Dtos;
using RedWineStock.Models;

namespace RedWineStock.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Roles.Staff)]
public class CategoriasController : ControllerBase
{
    private readonly AppDbContext _context;
    public CategoriasController(AppDbContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<PagedResult<CategoriaDto>>> GetAll(int page = 1, int pageSize = 100, bool incluirInactivas = false)
    {
        var query = _context.Categorias.AsNoTracking().AsQueryable();
        if (!incluirInactivas) query = query.Where(c => c.Activo);

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(c => c.Nombre)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CategoriaDto { IdCategoria = c.IdCategoria, Nombre = c.Nombre, Descripcion = c.Descripcion, Activo = c.Activo })
            .ToListAsync();

        return Ok(new PagedResult<CategoriaDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total });
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CategoriaDto>> GetById(int id)
    {
        var c = await _context.Categorias.AsNoTracking().FirstOrDefaultAsync(x => x.IdCategoria == id);
        if (c is null) return NotFound();
        return Ok(MapToDto(c));
    }

    [HttpPost]
    public async Task<ActionResult<CategoriaDto>> Create(CategoriaCreateDto dto)
    {
        var nombre = dto.Nombre.Trim();
        if (await _context.Categorias.AnyAsync(c => c.Nombre == nombre))
            return BadRequest("Ya existe una categoría con ese nombre.");

        var categoria = new Categoria { Nombre = nombre, Descripcion = dto.Descripcion, Activo = dto.Activo };
        _context.Categorias.Add(categoria);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = categoria.IdCategoria }, MapToDto(categoria));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, CategoriaCreateDto dto)
    {
        var categoria = await _context.Categorias.FindAsync(id);
        if (categoria is null) return NotFound();

        var nombre = dto.Nombre.Trim();
        if (await _context.Categorias.AnyAsync(c => c.Nombre == nombre && c.IdCategoria != id))
            return BadRequest("Ya existe una categoría con ese nombre.");

        categoria.Nombre = nombre;
        categoria.Descripcion = dto.Descripcion;
        categoria.Activo = dto.Activo;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(int id)
    {
        var categoria = await _context.Categorias.FindAsync(id);
        if (categoria is null) return NotFound();

        if (await _context.Productos.AnyAsync(p => p.IdCategoria == id))
            return Conflict("No se puede eliminar: la categoría tiene productos. Podés desactivarla.");

        _context.Categorias.Remove(categoria);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private static CategoriaDto MapToDto(Categoria c) =>
        new() { IdCategoria = c.IdCategoria, Nombre = c.Nombre, Descripcion = c.Descripcion, Activo = c.Activo };
}
