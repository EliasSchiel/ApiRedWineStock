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
[Authorize(Roles = Roles.Staff)]
public class ClientesController : ControllerBase
{
    private readonly AppDbContext _context;
    public ClientesController(AppDbContext context) => _context = context;

    /// <param name="soloConDeuda">true = solo clientes con ventas pendientes de pago</param>
    [HttpGet]
    public async Task<ActionResult<PagedResult<ClienteDto>>> GetAll(
        int page = 1, int pageSize = 20, string? busqueda = null, bool incluirInactivos = false, bool soloConDeuda = false)
    {
        var query = _context.Clientes.AsNoTracking().AsQueryable();

        if (!incluirInactivos) query = query.Where(c => c.Activo);
        if (soloConDeuda)
            query = query.Where(c => c.Ventas.Any(v => v.Estado == EstadoVenta.Pendiente && v.Total > v.MontoCobrado));
        if (!string.IsNullOrWhiteSpace(busqueda))
            query = query.Where(c => c.Nombre.Contains(busqueda) ||
                                     (c.Telefono != null && c.Telefono.Contains(busqueda)) ||
                                     (c.Email != null && c.Email.Contains(busqueda)));

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(c => c.Nombre)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new ClienteDto
            {
                IdCliente = c.IdCliente,
                Nombre = c.Nombre,
                Telefono = c.Telefono,
                Email = c.Email,
                Direccion = c.Direccion,
                Notas = c.Notas,
                Activo = c.Activo,
                Deuda = c.Ventas.Where(v => v.Estado == EstadoVenta.Pendiente).Sum(v => v.Total - v.MontoCobrado)
            })
            .ToListAsync();

        return Ok(new PagedResult<ClienteDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total });
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ClienteDto>> GetById(int id)
    {
        var dto = await ClienteConDeuda(id);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>Lo que debe el cliente y las ventas pendientes que componen esa deuda.</summary>
    [HttpGet("{id:int}/cuenta")]
    public async Task<ActionResult<ClienteCuentaDto>> GetCuenta(int id)
    {
        var cliente = await ClienteConDeuda(id);
        if (cliente is null) return NotFound();

        var ventas = await _context.Ventas.AsNoTracking()
            .Include(v => v.Usuario)
            .Include(v => v.Detalles)
            .Where(v => v.IdCliente == id && v.Estado == EstadoVenta.Pendiente)
            .OrderBy(v => v.Fecha)
            .ToListAsync();

        return Ok(new ClienteCuentaDto
        {
            Cliente = cliente,
            Deuda = cliente.Deuda,
            VentasPendientes = ventas.Select(v => new VentaResumenDto
            {
                IdVenta = v.IdVenta,
                Fecha = v.Fecha,
                IdCliente = v.IdCliente,
                ClienteNombre = cliente.Nombre,
                UsuarioNombre = v.Usuario?.Nombre,
                Total = v.Total,
                MontoCobrado = v.MontoCobrado,
                Saldo = v.Saldo,
                Estado = v.Estado.ToString(),
                CantidadItems = v.Detalles.Sum(d => d.Cantidad)
            }).ToList()
        });
    }

    [HttpPost]
    public async Task<ActionResult<ClienteDto>> Create(ClienteCreateDto dto)
    {
        var cliente = new Cliente();
        Copiar(dto, cliente);

        _context.Clientes.Add(cliente);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = cliente.IdCliente }, MapToDto(cliente, 0));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, ClienteCreateDto dto)
    {
        var cliente = await _context.Clientes.FindAsync(id);
        if (cliente is null) return NotFound();

        Copiar(dto, cliente);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(int id)
    {
        var cliente = await _context.Clientes.FindAsync(id);
        if (cliente is null) return NotFound();

        if (await _context.Ventas.AnyAsync(v => v.IdCliente == id))
            return Conflict("No se puede eliminar: el cliente tiene ventas registradas. Podés desactivarlo.");

        _context.Clientes.Remove(cliente);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private async Task<ClienteDto?> ClienteConDeuda(int id)
    {
        var c = await _context.Clientes.AsNoTracking().FirstOrDefaultAsync(x => x.IdCliente == id);
        if (c is null) return null;

        var deuda = await _context.Ventas
            .Where(v => v.IdCliente == id && v.Estado == EstadoVenta.Pendiente)
            .SumAsync(v => v.Total - v.MontoCobrado);
        return MapToDto(c, deuda);
    }

    private static void Copiar(ClienteCreateDto dto, Cliente c)
    {
        c.Nombre = dto.Nombre.Trim();
        c.Telefono = dto.Telefono;
        c.Email = dto.Email;
        c.Direccion = dto.Direccion;
        c.Notas = dto.Notas;
        c.Activo = dto.Activo;
    }

    private static ClienteDto MapToDto(Cliente c, decimal deuda) => new()
    {
        IdCliente = c.IdCliente,
        Nombre = c.Nombre,
        Telefono = c.Telefono,
        Email = c.Email,
        Direccion = c.Direccion,
        Notas = c.Notas,
        Activo = c.Activo,
        Deuda = deuda
    };
}
