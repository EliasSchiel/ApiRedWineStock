using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RedWineStock.Common;
using RedWineStock.Data;
using RedWineStock.Dtos;
using RedWineStock.Models;

namespace RedWineStock.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LogsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<LogsController> _logger;

    public LogsController(AppDbContext context, ILogger<LogsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// La app móvil informa un error. Es anónimo a propósito (para poder registrar fallas del login);
    /// si la app manda el token, el error queda asociado a ese usuario.
    /// </summary>
    [HttpPost("app")]
    [AllowAnonymous]
    public async Task<IActionResult> RegistrarErrorApp(AppLogDto dto)
    {
        // Plataforma y versión no tienen columna propia: se agregan al final del detalle
        var contexto = $"[Plataforma: {dto.Plataforma ?? "-"} | Versión de la app: {dto.VersionApp ?? "-"}]";
        var detalle = string.IsNullOrWhiteSpace(dto.Detalle) ? contexto : dto.Detalle + Environment.NewLine + Environment.NewLine + contexto;

        _context.LogErrores.Add(new LogError
        {
            Fecha = Reloj.Ahora,
            Mensaje = dto.Mensaje,
            Excepcion = detalle,
            Ruta = dto.Pantalla,
            IdUsuario = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null,
            Origen = OrigenLog.App
        });
        await _context.SaveChangesAsync();

        // Tambien al archivo de Serilog (sin saltos de linea, para que un cliente anonimo no pueda falsear lineas del log)
        _logger.LogWarning("Error informado por la app: {Mensaje} (pantalla: {Pantalla}, plataforma: {Plataforma}, version: {VersionApp})",
            dto.Mensaje.ReplaceLineEndings(" "), dto.Pantalla?.ReplaceLineEndings(" "),
            dto.Plataforma?.ReplaceLineEndings(" "), dto.VersionApp?.ReplaceLineEndings(" "));

        return NoContent();
    }
}
