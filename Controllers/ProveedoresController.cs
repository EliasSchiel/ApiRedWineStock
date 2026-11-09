using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RedWineStock.Common;
using RedWineStock.Data;
using RedWineStock.Dtos;
using RedWineStock.Models;

namespace RedWineStock.Controllers;

/// <summary>Proveedores y lo que se les debe. Solo Admin (el Operador no ve costos ni deudas).</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Roles.Admin)]
public class ProveedoresController : ControllerBase
{
    private readonly AppDbContext _context;
    public ProveedoresController(AppDbContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<PagedResult<ProveedorDto>>> GetAll(
        int page = 1, int pageSize = 20, string? busqueda = null, bool incluirInactivos = false)
    {
        var query = _context.Proveedores.AsNoTracking().AsQueryable();

        if (!incluirInactivos) query = query.Where(p => p.Activo);
        if (!string.IsNullOrWhiteSpace(busqueda))
            query = query.Where(p => p.Nombre.Contains(busqueda) || (p.Contacto != null && p.Contacto.Contains(busqueda)));

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(p => p.Nombre)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProveedorDto
            {
                IdProveedor = p.IdProveedor,
                Nombre = p.Nombre,
                Cuit = p.Cuit,
                Contacto = p.Contacto,
                Telefono = p.Telefono,
                Email = p.Email,
                Direccion = p.Direccion,
                Activo = p.Activo,
                Deuda = p.Ingresos.Sum(i => i.Total - i.MontoPagado)
            })
            .ToListAsync();

        return Ok(new PagedResult<ProveedorDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total });
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProveedorDto>> GetById(int id)
    {
        var p = await _context.Proveedores.AsNoTracking().FirstOrDefaultAsync(x => x.IdProveedor == id);
        if (p is null) return NotFound();

        var deuda = await _context.Ingresos.Where(i => i.IdProveedor == id).SumAsync(i => i.Total - i.MontoPagado);
        return Ok(MapToDto(p, deuda));
    }

    [HttpPost]
    public async Task<ActionResult<ProveedorDto>> Create(ProveedorCreateDto dto)
    {
        if (!string.IsNullOrWhiteSpace(dto.Cuit) && await _context.Proveedores.AnyAsync(p => p.Cuit == dto.Cuit))
            return BadRequest("Ya existe un proveedor con ese CUIT.");

        var proveedor = new Proveedor();
        Copiar(dto, proveedor);

        _context.Proveedores.Add(proveedor);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = proveedor.IdProveedor }, MapToDto(proveedor, 0));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, ProveedorCreateDto dto)
    {
        var proveedor = await _context.Proveedores.FindAsync(id);
        if (proveedor is null) return NotFound();

        if (!string.IsNullOrWhiteSpace(dto.Cuit) && await _context.Proveedores.AnyAsync(p => p.Cuit == dto.Cuit && p.IdProveedor != id))
            return BadRequest("Ya existe otro proveedor con ese CUIT.");

        Copiar(dto, proveedor);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var proveedor = await _context.Proveedores.FindAsync(id);
        if (proveedor is null) return NotFound();

        if (await _context.Ingresos.AnyAsync(i => i.IdProveedor == id) ||
            await _context.Productos.AnyAsync(p => p.IdProveedor == id))
            return Conflict("No se puede eliminar: el proveedor tiene ingresos o productos asociados. Podés desactivarlo.");

        _context.Proveedores.Remove(proveedor);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private static void Copiar(ProveedorCreateDto dto, Proveedor p)
    {
        p.Nombre = dto.Nombre.Trim();
        p.Cuit = string.IsNullOrWhiteSpace(dto.Cuit) ? null : dto.Cuit.Trim();
        p.Contacto = dto.Contacto;
        p.Telefono = dto.Telefono;
        p.Email = dto.Email;
        p.Direccion = dto.Direccion;
        p.Activo = dto.Activo;
    }

    private static ProveedorDto MapToDto(Proveedor p, decimal deuda) => new()
    {
        IdProveedor = p.IdProveedor,
        Nombre = p.Nombre,
        Cuit = p.Cuit,
        Contacto = p.Contacto,
        Telefono = p.Telefono,
        Email = p.Email,
        Direccion = p.Direccion,
        Activo = p.Activo,
        Deuda = deuda
    };
}
