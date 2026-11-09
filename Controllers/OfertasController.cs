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
/// Ofertas: descuento porcentual por producto y rango de fechas. Se aplican solas al registrar una venta.
/// Ver: Staff. Crear / modificar / dar de baja: Admin.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Roles.Staff)]
public class OfertasController : ControllerBase
{
    private readonly AppDbContext _context;
    public OfertasController(AppDbContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<PagedResult<OfertaDto>>> GetAll(
        int page = 1, int pageSize = 20, bool soloVigentes = false, int? idProducto = null)
    {
        var query = _context.Ofertas.AsNoTracking().Include(o => o.Producto).ThenInclude(p => p!.Marca).AsQueryable();
        var hoy = Reloj.Hoy;

        if (idProducto.HasValue) query = query.Where(o => o.IdProducto == idProducto);
        if (soloVigentes) query = query.Where(o => o.Activa && o.FechaDesde <= hoy && o.FechaHasta >= hoy);

        var total = await query.CountAsync();
        var ofertas = await query
            .OrderByDescending(o => o.FechaHasta).ThenBy(o => o.Nombre)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new PagedResult<OfertaDto>
        {
            Items = ofertas.Select(MapToDto).ToList(), Page = page, PageSize = pageSize, TotalCount = total
        });
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OfertaDto>> GetById(int id)
    {
        var o = await _context.Ofertas.AsNoTracking().Include(x => x.Producto).ThenInclude(p => p!.Marca).FirstOrDefaultAsync(x => x.IdOferta == id);
        return o is null ? NotFound() : Ok(MapToDto(o));
    }

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<OfertaDto>> Create(OfertaCreateDto dto)
    {
        var error = await Validar(dto);
        if (error is not null) return BadRequest(error);

        var oferta = new Oferta
        {
            IdProducto = dto.IdProducto,
            Nombre = dto.Nombre.Trim(),
            PorcentajeDescuento = dto.PorcentajeDescuento,
            FechaDesde = dto.FechaDesde.Date,
            FechaHasta = dto.FechaHasta.Date,
            Activa = true
        };
        _context.Ofertas.Add(oferta);
        await _context.SaveChangesAsync();
        await _context.Entry(oferta).Reference(o => o.Producto).LoadAsync();

        return CreatedAtAction(nameof(GetById), new { id = oferta.IdOferta }, MapToDto(oferta));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Update(int id, OfertaUpdateDto dto)
    {
        var oferta = await _context.Ofertas.FindAsync(id);
        if (oferta is null) return NotFound();

        var error = await Validar(dto);
        if (error is not null) return BadRequest(error);

        oferta.IdProducto = dto.IdProducto;
        oferta.Nombre = dto.Nombre.Trim();
        oferta.PorcentajeDescuento = dto.PorcentajeDescuento;
        oferta.FechaDesde = dto.FechaDesde.Date;
        oferta.FechaHasta = dto.FechaHasta.Date;
        oferta.Activa = dto.Activa;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    // Baja lógica: las ventas ya hechas guardan una referencia a la oferta aplicada
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(int id)
    {
        var oferta = await _context.Ofertas.FindAsync(id);
        if (oferta is null) return NotFound();

        oferta.Activa = false;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private async Task<string?> Validar(OfertaCreateDto dto)
    {
        if (dto.FechaHasta.Date < dto.FechaDesde.Date) return "La fecha 'hasta' no puede ser anterior a la fecha 'desde'.";
        if (!await _context.Productos.AnyAsync(p => p.IdProducto == dto.IdProducto && p.Activo))
            return "El producto indicado no existe o está inactivo.";
        return null;
    }

    private static OfertaDto MapToDto(Oferta o) => new()
    {
        IdOferta = o.IdOferta,
        IdProducto = o.IdProducto,
        ProductoNombre = o.Producto?.Nombre,
        ProductoMarca = o.Producto?.Marca?.Nombre,
        ProductoCosecha = o.Producto?.Cosecha,
        Nombre = o.Nombre,
        PorcentajeDescuento = o.PorcentajeDescuento,
        PrecioConOferta = o.Producto is null ? 0 : OfertaService.AplicarDescuento(o.Producto.PrecioVenta, o.PorcentajeDescuento),
        FechaDesde = o.FechaDesde,
        FechaHasta = o.FechaHasta,
        Activa = o.Activa,
        Vigente = o.EstaVigente(Reloj.Hoy)
    };
}
