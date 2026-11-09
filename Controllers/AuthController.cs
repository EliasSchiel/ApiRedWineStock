using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RedWineStock.Common;
using RedWineStock.Data;
using RedWineStock.Dtos;
using RedWineStock.Models;
using RedWineStock.Services;

namespace RedWineStock.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _config;
    private readonly AuditoriaService _auditoria;

    public AuthController(AppDbContext context, IConfiguration config, AuditoriaService auditoria)
    {
        _context = context;
        _config = config;
        _auditoria = auditoria;
    }

    /// <summary>Inicio de sesión con email y contraseña. Devuelve el token JWT que la app manda en cada pedido.</summary>
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponseDto>> Login(LoginDto dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();

        var usuario = await _context.Usuarios
            .Include(u => u.Rol)
            .FirstOrDefaultAsync(u => u.Email == email && u.Activo);

        if (usuario is null || !BCrypt.Net.BCrypt.Verify(dto.Password, usuario.PasswordHash))
        {
            await _auditoria.RegistrarAsync("LoginFallido", nameof(Usuario), detalle: $"Email: {email}");
            return Unauthorized("Email o contraseña incorrectos.");
        }

        await _auditoria.RegistrarAsync("Login", nameof(Usuario), usuario.IdUsuario.ToString(), idUsuario: usuario.IdUsuario);
        return Ok(GenerarToken(usuario));
    }

    /// <summary>Datos del usuario logueado (útil para la app al iniciar, con el token ya guardado).</summary>
    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UsuarioDto>> Me()
    {
        var u = await _context.Usuarios.Include(x => x.Rol).AsNoTracking()
            .FirstOrDefaultAsync(x => x.IdUsuario == User.IdUsuario() && x.Activo);
        if (u is null) return Unauthorized();

        return Ok(new UsuarioDto
        {
            IdUsuario = u.IdUsuario, Nombre = u.Nombre, Email = u.Email,
            Rol = u.Rol!.Nombre, Activo = u.Activo, FechaAlta = u.FechaAlta
        });
    }

    [Authorize]
    [HttpPut("cambiar-password")]
    public async Task<IActionResult> CambiarPassword(CambiarPasswordDto dto)
    {
        var usuario = await _context.Usuarios.FindAsync(User.IdUsuario());
        if (usuario is null) return NotFound();

        if (!BCrypt.Net.BCrypt.Verify(dto.PasswordActual, usuario.PasswordHash))
            return BadRequest("La contraseña actual no es correcta.");

        usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.PasswordNueva);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private LoginResponseDto GenerarToken(Usuario usuario)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, usuario.IdUsuario.ToString()),
            new(ClaimTypes.Name, usuario.Nombre),
            new(ClaimTypes.Email, usuario.Email),
            new(ClaimTypes.Role, usuario.Rol!.Nombre)
        };

        var expira = DateTime.UtcNow.AddMinutes(double.Parse(_config["Jwt:ExpiresInMinutes"] ?? "480"));

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: expira,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!)),
                SecurityAlgorithms.HmacSha256));

        return new LoginResponseDto
        {
            Token = new JwtSecurityTokenHandler().WriteToken(token),
            Expira = expira,
            IdUsuario = usuario.IdUsuario,
            Nombre = usuario.Nombre,
            Email = usuario.Email,
            Rol = usuario.Rol.Nombre
        };
    }
}
