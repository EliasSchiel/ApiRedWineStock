using RedWineStock.Common;
using RedWineStock.Data;
using RedWineStock.Models;

namespace RedWineStock.Services;

/// <summary>
/// Para registrar acciones que no son cambios de datos (ej: login, login fallido).
/// Los altas/modificaciones/bajas se auditan solos desde AppDbContext.
/// </summary>
public class AuditoriaService
{
    private readonly AppDbContext _context;
    private readonly IHttpContextAccessor _http;

    public AuditoriaService(AppDbContext context, IHttpContextAccessor http)
    {
        _context = context;
        _http = http;
    }

    public async Task RegistrarAsync(string accion, string entidad, string? idEntidad = null, string? detalle = null, int? idUsuario = null)
    {
        _context.Auditorias.Add(new Auditoria
        {
            Fecha = Reloj.Ahora,
            IdUsuario = idUsuario,
            Accion = accion,
            Entidad = entidad,
            IdEntidad = idEntidad,
            Detalle = detalle,
            Ip = _http.HttpContext?.Connection.RemoteIpAddress?.ToString()
        });
        await _context.SaveChangesAsync();
    }
}
