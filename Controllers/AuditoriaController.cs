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
[Authorize(Roles = Roles.Admin)]
public class AuditoriaController : ControllerBase
{
    private readonly AppDbContext _context;
    public AuditoriaController(AppDbContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<PagedResult<AuditoriaDto>>> GetAuditoria(
        int page = 1, int pageSize = 20, string? entidad = null, string? accion = null,
        int? idUsuario = null, DateTime? desde = null, DateTime? hasta = null)
    {
        var query = _context.Auditorias.AsNoTracking().AsQueryable();
        // Sin filtro, se ocultan las líneas de venta/ingreso: ya están dentro del registro de la venta o el ingreso
        if (!string.IsNullOrWhiteSpace(entidad)) query = query.Where(a => a.Entidad == entidad);
        else query = query.Where(a => a.Entidad != nameof(DetalleVenta) && a.Entidad != nameof(DetalleIngreso) && a.Entidad != nameof(ComboItem));
        if (!string.IsNullOrWhiteSpace(accion)) query = query.Where(a => a.Accion == accion);
        if (idUsuario.HasValue) query = query.Where(a => a.IdUsuario == idUsuario);
        if (desde.HasValue) query = query.Where(a => a.Fecha >= desde.Value.Date);
        if (hasta.HasValue) query = query.Where(a => a.Fecha < hasta.Value.Date.AddDays(1));

        var total = await query.CountAsync();
        // Sin FK a Usuario: el nombre se busca aparte (queda vacío si el usuario ya no existe)
        var items = await query
            .OrderByDescending(a => a.Fecha).ThenByDescending(a => a.IdAuditoria)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new AuditoriaDto
            {
                IdAuditoria = a.IdAuditoria,
                Fecha = a.Fecha,
                IdUsuario = a.IdUsuario,
                UsuarioNombre = _context.Usuarios.Where(u => u.IdUsuario == a.IdUsuario).Select(u => u.Nombre).FirstOrDefault(),
                Accion = a.Accion,
                Entidad = a.Entidad,
                IdEntidad = a.IdEntidad,
                Detalle = a.Detalle,
                Ip = a.Ip
            })
            .ToListAsync();
        return Ok(new PagedResult<AuditoriaDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total });
    }

    [HttpGet("errores")]
    public async Task<ActionResult<PagedResult<LogErrorDto>>> GetErrores(
        int page = 1, int pageSize = 20, DateTime? desde = null, DateTime? hasta = null, string? origen = null)
    {
        var query = _context.LogErrores.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(origen)) query = query.Where(l => l.Origen == origen);
        if (desde.HasValue) query = query.Where(l => l.Fecha >= desde.Value.Date);
        if (hasta.HasValue) query = query.Where(l => l.Fecha < hasta.Value.Date.AddDays(1));

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(l => l.Fecha).ThenByDescending(l => l.IdLogError)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(l => new LogErrorDto
            {
                IdLogError = l.IdLogError,
                Fecha = l.Fecha,
                Mensaje = l.Mensaje,
                Excepcion = l.Excepcion,
                Ruta = l.Ruta,
                MetodoHttp = l.MetodoHttp,
                IdUsuario = l.IdUsuario,
                UsuarioNombre = _context.Usuarios.Where(u => u.IdUsuario == l.IdUsuario).Select(u => u.Nombre).FirstOrDefault(),
                Origen = l.Origen
            })
            .ToListAsync();
        return Ok(new PagedResult<LogErrorDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total });
    }
}
