using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RedWineStock.Common;
using RedWineStock.Data;
using RedWineStock.Dtos;
using RedWineStock.Models;

namespace RedWineStock.Controllers;

/// <summary>Alta y administración de las cuentas del personal. Solo Admin.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Roles.Admin)]
public class UsuariosController : ControllerBase
{
    private readonly AppDbContext _context;
    public UsuariosController(AppDbContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<PagedResult<UsuarioDto>>> GetAll(
        int page = 1, int pageSize = 20, string? rol = null, string? busqueda = null, bool incluirInactivos = true)
    {
        var query = _context.Usuarios.AsNoTracking().AsQueryable();
        if (!incluirInactivos) query = query.Where(u => u.Activo);
        if (!string.IsNullOrWhiteSpace(rol)) query = query.Where(u => u.Rol!.Nombre == rol);
        if (!string.IsNullOrWhiteSpace(busqueda))
            query = query.Where(u => u.Nombre.Contains(busqueda) || u.Email.Contains(busqueda));

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(u => u.Nombre)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UsuarioDto
            {
                IdUsuario = u.IdUsuario, Nombre = u.Nombre, Email = u.Email,
                Rol = u.Rol!.Nombre, Activo = u.Activo, FechaAlta = u.FechaAlta
            })
            .ToListAsync();

        return Ok(new PagedResult<UsuarioDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total });
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<UsuarioDto>> GetById(int id)
    {
        var u = await _context.Usuarios.Include(x => x.Rol).AsNoTracking().FirstOrDefaultAsync(x => x.IdUsuario == id);
        if (u is null) return NotFound();
        return Ok(MapToDto(u));
    }

    [HttpGet("roles")]
    public async Task<ActionResult<List<RolDto>>> GetRoles() =>
        Ok(await _context.Roles.AsNoTracking().OrderBy(r => r.IdRol)
            .Select(r => new RolDto { IdRol = r.IdRol, Nombre = r.Nombre }).ToListAsync());

    [HttpPost]
    public async Task<ActionResult<UsuarioDto>> Create(UsuarioCreateDto dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        if (await _context.Usuarios.AnyAsync(u => u.Email == email))
            return BadRequest("Ya existe un usuario con ese email.");

        var rol = await _context.Roles.FirstOrDefaultAsync(r => r.Nombre == dto.Rol);
        if (rol is null) return BadRequest("Rol inválido. Valores posibles: Admin, Operador.");

        var usuario = new Usuario
        {
            Nombre = dto.Nombre.Trim(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            IdRol = rol.IdRol
        };

        _context.Usuarios.Add(usuario);
        await _context.SaveChangesAsync();
        usuario.Rol = rol;

        return CreatedAtAction(nameof(GetById), new { id = usuario.IdUsuario }, MapToDto(usuario));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UsuarioUpdateDto dto)
    {
        var usuario = await _context.Usuarios.FindAsync(id);
        if (usuario is null) return NotFound();

        var email = dto.Email.Trim().ToLowerInvariant();
        if (await _context.Usuarios.AnyAsync(u => u.Email == email && u.IdUsuario != id))
            return BadRequest("Ya existe otro usuario con ese email.");

        var rol = await _context.Roles.FirstOrDefaultAsync(r => r.Nombre == dto.Rol);
        if (rol is null) return BadRequest("Rol inválido. Valores posibles: Admin, Operador.");

        // Evita que el administrador se deje a sí mismo sin acceso
        if (id == User.IdUsuario() && (!dto.Activo || rol.IdRol != Roles.IdAdmin))
            return BadRequest("No podés desactivar tu propia cuenta ni quitarte el rol de administrador.");

        usuario.Nombre = dto.Nombre.Trim();
        usuario.Email = email;
        usuario.IdRol = rol.IdRol;
        usuario.Activo = dto.Activo;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>El Admin define una contraseña nueva (para quien olvidó la suya).</summary>
    [HttpPut("{id:int}/password")]
    public async Task<IActionResult> ResetPassword(int id, ResetPasswordDto dto)
    {
        var usuario = await _context.Usuarios.FindAsync(id);
        if (usuario is null) return NotFound();

        usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.PasswordNueva);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // Baja lógica: el usuario puede figurar en ventas e ingresos históricos
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (id == User.IdUsuario()) return BadRequest("No podés dar de baja tu propia cuenta.");

        var usuario = await _context.Usuarios.FindAsync(id);
        if (usuario is null) return NotFound();

        usuario.Activo = false;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private static UsuarioDto MapToDto(Usuario u) => new()
    {
        IdUsuario = u.IdUsuario, Nombre = u.Nombre, Email = u.Email,
        Rol = u.Rol!.Nombre, Activo = u.Activo, FechaAlta = u.FechaAlta
    };
}
