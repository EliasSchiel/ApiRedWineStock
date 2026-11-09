using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RedWineStock.Common;
using RedWineStock.Data;
using RedWineStock.Dtos;

namespace RedWineStock.Controllers;

/// <summary>Lista fija de medios de pago (se carga por seed): efectivo, débito, crédito, Mercado Pago, transferencia.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Roles.Staff)]
public class MediosPagoController : ControllerBase
{
    private readonly AppDbContext _context;
    public MediosPagoController(AppDbContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<List<MedioPagoDto>>> GetAll() =>
        Ok(await _context.MediosPago.AsNoTracking().Where(m => m.Activo).OrderBy(m => m.IdMedioPago)
            .Select(m => new MedioPagoDto { IdMedioPago = m.IdMedioPago, Nombre = m.Nombre }).ToListAsync());
}
