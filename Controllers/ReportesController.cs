using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RedWineStock.Common;
using RedWineStock.Data;
using RedWineStock.Dtos;
using RedWineStock.Models;
using RedWineStock.Services;

namespace RedWineStock.Controllers;

/// <summary>
/// Reportes en PDF. Se pueden descargar (GET) o enviar por mail como adjunto (POST api/reportes/{tipo}/enviar).
/// Admin y Operador: stock, stock-bajo, ventas, mas-vendidos, deudores.
/// Solo Admin (muestran costos / deudas con proveedores): ingresos, deudas-proveedores.
/// En los reportes con fechas, si no se indican se usa el mes actual; si se indica solo una, el mes de esa fecha.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Roles.Staff)]
public class ReportesController : ControllerBase
{
    private readonly ReporteService _reportes;
    private readonly IEmailService _email;
    private readonly AuditoriaService _auditoria;
    private readonly AppDbContext _context;
    private readonly ILogger<ReportesController> _logger;

    public ReportesController(ReporteService reportes, IEmailService email, AuditoriaService auditoria, AppDbContext context,
        ILogger<ReportesController> logger)
    {
        _logger = logger;
        _reportes = reportes;
        _email = email;
        _auditoria = auditoria;
        _context = context;
    }

    /// <summary>Productos activos con su stock, valor (stock × precio de venta) y marca de stock bajo.</summary>
    [HttpGet("stock")]
    public Task<IActionResult> Stock(int? idCategoria = null) =>
        Descargar(ReporteTipos.Stock, new ReporteFiltros(IdCategoria: idCategoria));

    /// <summary>Productos activos con Stock menor o igual al stock mínimo (lista para pedir a proveedores).</summary>
    [HttpGet("stock-bajo")]
    public Task<IActionResult> StockBajo(int? idCategoria = null) =>
        Descargar(ReporteTipos.StockBajo, new ReporteFiltros(IdCategoria: idCategoria));

    /// <summary>Ventas del período con cliente, estado, total, cobrado y saldo.</summary>
    [HttpGet("ventas")]
    public Task<IActionResult> Ventas(DateTime? desde = null, DateTime? hasta = null, EstadoVenta? estado = null) =>
        Descargar(ReporteTipos.Ventas, new ReporteFiltros(Desde: desde, Hasta: hasta, Estado: estado));

    /// <summary>Ranking de productos más vendidos del período.</summary>
    [HttpGet("mas-vendidos")]
    public Task<IActionResult> MasVendidos(DateTime? desde = null, DateTime? hasta = null) =>
        Descargar(ReporteTipos.MasVendidos, new ReporteFiltros(Desde: desde, Hasta: hasta));

    /// <summary>Clientes que deben plata y cuánto.</summary>
    [HttpGet("deudores")]
    public Task<IActionResult> Deudores() =>
        Descargar(ReporteTipos.Deudores, new ReporteFiltros());

    /// <summary>[Admin] Ingresos de mercadería del período con total, pagado y saldo.</summary>
    [HttpGet("ingresos")]
    public Task<IActionResult> Ingresos(DateTime? desde = null, DateTime? hasta = null, int? idProveedor = null) =>
        Descargar(ReporteTipos.Ingresos, new ReporteFiltros(Desde: desde, Hasta: hasta, IdProveedor: idProveedor));

    /// <summary>[Admin] Lo que se le debe a cada proveedor.</summary>
    [HttpGet("deudas-proveedores")]
    public Task<IActionResult> DeudasProveedores() =>
        Descargar(ReporteTipos.DeudasProveedores, new ReporteFiltros());

    /// <summary>
    /// Genera el reporte (tipo = stock | stock-bajo | ventas | mas-vendidos | deudores | ingresos | deudas-proveedores)
    /// y lo envía por mail como PDF adjunto. Si el servidor SMTP falla responde 502 y el error queda en LogErrores.
    /// </summary>
    [HttpPost("{tipo}/enviar")]
    public async Task<IActionResult> Enviar(string tipo, EnviarReporteDto dto)
    {
        tipo = tipo.ToLowerInvariant();
        var filtros = new ReporteFiltros(dto.Desde, dto.Hasta, dto.IdCategoria, dto.IdProveedor, dto.Estado);

        var (reporte, error) = await PrepararAsync(tipo, filtros);
        if (error is not null) return error;

        var asunto = $"Reporte: {reporte!.Titulo}";
        var cuerpo =
            $"Hola,{Environment.NewLine}{Environment.NewLine}" +
            $"Te enviamos adjunto el reporte \"{reporte.Titulo}\" generado el {Reloj.Ahora:dd/MM/yyyy HH:mm}.{Environment.NewLine}{Environment.NewLine}" +
            "Este mensaje fue generado automáticamente por RedWine Stock.";

        try
        {
            await _email.EnviarConAdjuntoAsync(dto.Email, asunto, cuerpo, reporte.NombreArchivo, reporte.Contenido);
        }
        catch (EmailException ex)
        {
            var idLog = await RegistrarErrorAsync(ex, tipo);

            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status502BadGateway,
                Title = "No se pudo enviar el reporte por mail.",
                Detail = ex.Message,
                Instance = Request.Path
            };
            if (idLog.HasValue) problem.Extensions["idLogError"] = idLog.Value;
            return StatusCode(StatusCodes.Status502BadGateway, problem);
        }

        await _auditoria.RegistrarAsync("EnvioReporte", "Reporte", tipo, detalle: dto.Email, idUsuario: IdUsuarioLogueado());
        _logger.LogInformation("Reporte {Tipo} enviado por mail (usuario {IdUsuario})", tipo, IdUsuarioLogueado());
        return Ok(new { mensaje = $"El reporte se envió a {dto.Email}." });
    }

    // ================== AUXILIARES ==================

    private async Task<IActionResult> Descargar(string tipo, ReporteFiltros filtros)
    {
        var (reporte, error) = await PrepararAsync(tipo, filtros);
        if (error is not null) return error;
        return File(reporte!.Contenido, "application/pdf", reporte.NombreArchivo);
    }

    /// <summary>Valida permisos, tipo y filtros y genera el PDF. Devuelve el reporte o el error a responder.</summary>
    private async Task<(ReporteArchivo? Reporte, IActionResult? Error)> PrepararAsync(string tipo, ReporteFiltros filtros)
    {
        if (!ReporteTipos.Todos.Contains(tipo))
            return (null, BadRequest($"Tipo de reporte inválido. Valores posibles: {string.Join(", ", ReporteTipos.Todos)}."));

        if (ReporteTipos.SoloAdmin.Contains(tipo) && !User.EsAdmin())
            return (null, StatusCode(StatusCodes.Status403Forbidden, "Este reporte muestra costos o deudas con proveedores y solo lo puede pedir un administrador."));

        if (tipo is ReporteTipos.Ingresos or ReporteTipos.Ventas or ReporteTipos.MasVendidos)
        {
            var (desde, hasta) = ReporteService.ResolverRango(filtros.Desde, filtros.Hasta);
            if (desde > hasta)
                return (null, BadRequest("La fecha 'desde' no puede ser posterior a la fecha 'hasta'."));
        }

        if (filtros.IdCategoria.HasValue && (tipo == ReporteTipos.Stock || tipo == ReporteTipos.StockBajo) &&
            !await _context.Categorias.AnyAsync(c => c.IdCategoria == filtros.IdCategoria))
            return (null, BadRequest("La categoría indicada no existe."));

        if (filtros.IdProveedor.HasValue && tipo == ReporteTipos.Ingresos &&
            !await _context.Proveedores.AnyAsync(p => p.IdProveedor == filtros.IdProveedor))
            return (null, BadRequest("El proveedor indicado no existe."));

        var reporte = await _reportes.GenerarAsync(tipo, filtros);
        return (reporte, null);
    }

    /// <summary>Guarda en LogErrores la falla de envío (origen API). Nunca debe romper la respuesta.</summary>
    private async Task<int?> RegistrarErrorAsync(Exception ex, string tipo)
    {
        try
        {
            var mensaje = $"Falló el envío por mail del reporte '{tipo}': {ex.Message}";
            var log = new LogError
            {
                Fecha = Reloj.Ahora,
                Mensaje = mensaje.Length <= 2000 ? mensaje : mensaje[..2000],
                Excepcion = ex.ToString(),
                Ruta = Request.Path.Value,
                MetodoHttp = Request.Method,
                IdUsuario = IdUsuarioLogueado(),
                Origen = OrigenLog.Api
            };
            _context.LogErrores.Add(log);
            await _context.SaveChangesAsync();
            return log.IdLogError;
        }
        catch
        {
            return null;
        }
    }

    private int? IdUsuarioLogueado() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
