using System.Security.Claims;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using RedWineStock.Common;
using RedWineStock.Data;
using RedWineStock.Models;

namespace RedWineStock.Middleware;

/// <summary>
/// Atrapa cualquier excepción no controlada, la guarda en la tabla LogErrores
/// y devuelve un 500 en formato JSON (ProblemDetails) en lugar de una página HTML.
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionHandler(IServiceScopeFactory scopeFactory, ILogger<GlobalExceptionHandler> logger, IHostEnvironment env)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _env = env;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Error no controlado en {Ruta}", httpContext.Request.Path);

        int? idLog = null;
        try
        {
            // Scope nuevo: el DbContext del request puede haber quedado en mal estado
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var idUsuarioStr = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var log = new LogError
            {
                Fecha = Reloj.Ahora,
                Mensaje = Recortar(exception.Message, 2000),
                Excepcion = exception.ToString(),
                Ruta = Recortar(httpContext.Request.Path + httpContext.Request.QueryString, 300),
                MetodoHttp = httpContext.Request.Method,
                IdUsuario = int.TryParse(idUsuarioStr, out var id) ? id : null,
                Origen = OrigenLog.Api
            };
            db.LogErrores.Add(log);
            await db.SaveChangesAsync(cancellationToken);
            idLog = log.IdLogError;
        }
        catch (Exception exLog)
        {
            // Si no se puede guardar en la BD (ej: la BD está caída) queda al menos en el log del servidor
            _logger.LogError(exLog, "No se pudo registrar el error en la base de datos");
        }

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Ocurrió un error inesperado.",
            Detail = _env.IsDevelopment() ? exception.Message : "El error fue registrado. Si persiste, contactá al administrador.",
            Instance = httpContext.Request.Path
        };
        if (idLog.HasValue) problem.Extensions["idLogError"] = idLog.Value;

        httpContext.Response.StatusCode = problem.Status.Value;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }

    private static string Recortar(string texto, int max) => texto.Length <= max ? texto : texto[..max];
}
