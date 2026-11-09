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
/// Catálogo de productos con su stock. Todo el personal puede ver, crear y editar;
/// el costo (PrecioCompra) y el proveedor habitual solo los ve y los define el Admin.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Roles.Staff)]
public class ProductosController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ImagenService _imagenes;
    private readonly OfertaService _ofertas;

    public ProductosController(AppDbContext context, ImagenService imagenes, OfertaService ofertas)
    {
        _context = context;
        _imagenes = imagenes;
        _ofertas = ofertas;
    }

    // ===================== CONSULTAS =====================

    /// <param name="busqueda">busca en nombre, código, cepa y marca</param>
    /// <param name="orden">nombre | precio_asc | precio_desc | stock_asc | stock_desc | recientes</param>
    [HttpGet]
    public async Task<ActionResult<PagedResult<ProductoDto>>> GetAll(
        int page = 1, int pageSize = 20, string? busqueda = null,
        int? idCategoria = null, int? idMarca = null, int? idProveedor = null,
        bool soloStockBajo = false, bool soloConStock = false, bool incluirInactivos = false, string? orden = null)
    {
        var query = _context.Productos.AsNoTracking().AsQueryable();

        if (!incluirInactivos) query = query.Where(p => p.Activo);
        if (soloStockBajo) query = query.Where(p => p.Stock <= p.StockMinimo);
        if (soloConStock) query = query.Where(p => p.Stock > 0);
        if (idCategoria.HasValue) query = query.Where(p => p.IdCategoria == idCategoria);
        if (idMarca.HasValue) query = query.Where(p => p.IdMarca == idMarca);
        if (idProveedor.HasValue && User.EsAdmin()) query = query.Where(p => p.IdProveedor == idProveedor);
        if (!string.IsNullOrWhiteSpace(busqueda))
            query = query.Where(p => p.Nombre.Contains(busqueda) ||
                                     (p.Codigo != null && p.Codigo.Contains(busqueda)) ||
                                     (p.Cepa != null && p.Cepa.Contains(busqueda)) ||
                                     (p.Marca != null && p.Marca.Nombre.Contains(busqueda)));

        query = orden switch
        {
            "precio_asc" => query.OrderBy(p => p.PrecioVenta),
            "precio_desc" => query.OrderByDescending(p => p.PrecioVenta),
            "stock_asc" => query.OrderBy(p => p.Stock).ThenBy(p => p.Nombre),
            "stock_desc" => query.OrderByDescending(p => p.Stock).ThenBy(p => p.Nombre),
            "recientes" => query.OrderByDescending(p => p.FechaCreacion),
            _ => query.OrderBy(p => p.Nombre)
        };

        var total = await query.CountAsync();
        var productos = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(p => p.Categoria)
            .Include(p => p.Marca)
            .Include(p => p.Proveedor)
            .Include(p => p.Imagenes)
            .ToListAsync();

        var ofertas = await _ofertas.VigentesAsync(productos.Select(p => p.IdProducto));

        return Ok(new PagedResult<ProductoDto>
        {
            Items = productos.Select(p => MapToDto(p, ofertas)).ToList(),
            Page = page, PageSize = pageSize, TotalCount = total
        });
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductoDto>> GetById(int id)
    {
        var p = await CargarAsync(id);
        return p is null ? NotFound() : Ok(await MapUnoAsync(p));
    }

    /// <summary>Busca por código exacto (código de barras): lo usará el lector en la pantalla de venta.</summary>
    [HttpGet("por-codigo/{codigo}")]
    public async Task<ActionResult<ProductoDto>> GetByCodigo(string codigo)
    {
        var p = await _context.Productos.AsNoTracking()
            .Include(x => x.Categoria).Include(x => x.Marca).Include(x => x.Proveedor).Include(x => x.Imagenes)
            .FirstOrDefaultAsync(x => x.Codigo == codigo && x.Activo);
        return p is null ? NotFound() : Ok(await MapUnoAsync(p));
    }

    /// <summary>Productos activos con stock en o por debajo del mínimo (alertas).</summary>
    [HttpGet("stock-bajo")]
    public async Task<ActionResult<List<ProductoDto>>> GetStockBajo()
    {
        var productos = await _context.Productos.AsNoTracking()
            .Where(p => p.Activo && p.Stock <= p.StockMinimo)
            .Include(p => p.Categoria).Include(p => p.Marca).Include(p => p.Proveedor).Include(p => p.Imagenes)
            .OrderBy(p => p.Stock).ThenBy(p => p.Nombre)
            .ToListAsync();

        var ofertas = await _ofertas.VigentesAsync(productos.Select(p => p.IdProducto));
        return Ok(productos.Select(p => MapToDto(p, ofertas)).ToList());
    }

    // ===================== ABM =====================

    [HttpPost]
    public async Task<ActionResult<ProductoDto>> Create(ProductoCreateDto dto)
    {
        var error = await Validar(dto, null);
        if (error is not null) return BadRequest(error);

        var producto = new Producto { Stock = 0 };
        Copiar(dto, producto);
        if (User.EsAdmin())
        {
            producto.PrecioCompra = dto.PrecioCompra;
            producto.IdProveedor = dto.IdProveedor;
        }

        _context.Productos.Add(producto);
        await _context.SaveChangesAsync();

        var creado = await CargarAsync(producto.IdProducto);
        return CreatedAtAction(nameof(GetById), new { id = producto.IdProducto }, await MapUnoAsync(creado!));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, ProductoUpdateDto dto)
    {
        var producto = await _context.Productos.FindAsync(id);
        if (producto is null) return NotFound();

        var error = await Validar(dto, id);
        if (error is not null) return BadRequest(error);

        Copiar(dto, producto);
        producto.Activo = dto.Activo;
        if (User.EsAdmin())
        {
            producto.PrecioCompra = dto.PrecioCompra;
            producto.IdProveedor = dto.IdProveedor;
        }

        await _context.SaveChangesAsync();
        return NoContent();
    }

    // Baja lógica (el producto puede estar en ventas/ingresos históricos)
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(int id)
    {
        var producto = await _context.Productos.FindAsync(id);
        if (producto is null) return NotFound();

        producto.Activo = false;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // ===================== IMÁGENES =====================

    /// <summary>
    /// Sube una imagen al producto. Postman: Body > form-data > clave "archivo" (tipo File).
    /// La primera imagen que se sube queda como principal.
    /// </summary>
    [HttpPost("{id:int}/imagenes")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ImagenService.TamanioMaximo + 1024 * 1024)]
    public async Task<ActionResult<ProductoImagenDto>> SubirImagen(int id, IFormFile? archivo, [FromForm] bool esPrincipal = false)
    {
        var producto = await _context.Productos.Include(p => p.Imagenes).FirstOrDefaultAsync(p => p.IdProducto == id);
        if (producto is null) return NotFound();

        var (ok, nombreArchivo, error) = await _imagenes.GuardarAsync(archivo);
        if (!ok) return BadRequest(error);

        var esLaPrimera = producto.Imagenes.Count == 0;
        if (esPrincipal && !esLaPrimera)
            foreach (var img in producto.Imagenes) img.EsPrincipal = false;

        var imagen = new ProductoImagen
        {
            NombreArchivo = nombreArchivo!,
            EsPrincipal = esPrincipal || esLaPrimera,
            Orden = esLaPrimera ? 0 : producto.Imagenes.Max(i => i.Orden) + 1
        };
        producto.Imagenes.Add(imagen);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch
        {
            _imagenes.Eliminar(nombreArchivo!);   // no dejar archivos huérfanos
            throw;
        }

        return Ok(MapImagen(imagen));
    }

    [HttpPut("{id:int}/imagenes/{idImagen:int}/principal")]
    public async Task<IActionResult> MarcarPrincipal(int id, int idImagen)
    {
        var imagenes = await _context.ProductoImagenes.Where(i => i.IdProducto == id).ToListAsync();
        if (!imagenes.Any(i => i.IdProductoImagen == idImagen)) return NotFound();

        foreach (var img in imagenes) img.EsPrincipal = img.IdProductoImagen == idImagen;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}/imagenes/{idImagen:int}")]
    public async Task<IActionResult> EliminarImagen(int id, int idImagen)
    {
        var imagen = await _context.ProductoImagenes.FirstOrDefaultAsync(i => i.IdProducto == id && i.IdProductoImagen == idImagen);
        if (imagen is null) return NotFound();

        _context.ProductoImagenes.Remove(imagen);

        // Si era la principal, pasa a ser principal la siguiente
        if (imagen.EsPrincipal)
        {
            var siguiente = await _context.ProductoImagenes
                .Where(i => i.IdProducto == id && i.IdProductoImagen != idImagen)
                .OrderBy(i => i.Orden).FirstOrDefaultAsync();
            if (siguiente is not null) siguiente.EsPrincipal = true;
        }

        await _context.SaveChangesAsync();
        _imagenes.Eliminar(imagen.NombreArchivo);
        return NoContent();
    }

    // ===================== Helpers =====================

    private async Task<string?> Validar(ProductoCreateDto dto, int? idActual)
    {
        if (!await _context.Categorias.AnyAsync(c => c.IdCategoria == dto.IdCategoria && c.Activo))
            return "La categoría indicada no existe o está inactiva.";

        if (dto.IdMarca.HasValue && !await _context.Marcas.AnyAsync(m => m.IdMarca == dto.IdMarca))
            return "La marca indicada no existe.";

        if (User.EsAdmin() && dto.IdProveedor.HasValue && !await _context.Proveedores.AnyAsync(p => p.IdProveedor == dto.IdProveedor))
            return "El proveedor indicado no existe.";

        var codigo = string.IsNullOrWhiteSpace(dto.Codigo) ? null : dto.Codigo.Trim();
        if (codigo is not null && await _context.Productos.AnyAsync(p => p.Codigo == codigo && p.IdProducto != idActual))
            return "Ya existe un producto con ese código.";

        return null;
    }

    private static void Copiar(ProductoCreateDto dto, Producto p)
    {
        p.Codigo = string.IsNullOrWhiteSpace(dto.Codigo) ? null : dto.Codigo.Trim();
        p.Nombre = dto.Nombre.Trim();
        p.Descripcion = dto.Descripcion;
        p.Cepa = dto.Cepa;
        p.Cosecha = dto.Cosecha;
        p.VolumenMl = dto.VolumenMl;
        p.UnidadesPorCaja = dto.UnidadesPorCaja;
        p.PrecioVenta = dto.PrecioVenta;
        p.StockMinimo = dto.StockMinimo;
        p.IdCategoria = dto.IdCategoria;
        p.IdMarca = dto.IdMarca;
    }

    private Task<Producto?> CargarAsync(int id) => _context.Productos.AsNoTracking()
        .Include(x => x.Categoria).Include(x => x.Marca).Include(x => x.Proveedor).Include(x => x.Imagenes)
        .FirstOrDefaultAsync(x => x.IdProducto == id);

    private async Task<ProductoDto> MapUnoAsync(Producto p)
    {
        var ofertas = await _ofertas.VigentesAsync(new[] { p.IdProducto });
        return MapToDto(p, ofertas);
    }

    private ProductoImagenDto MapImagen(ProductoImagen i) => new()
    {
        IdProductoImagen = i.IdProductoImagen,
        Url = ImagenService.ArmarUrl(Request, i.NombreArchivo),
        EsPrincipal = i.EsPrincipal,
        Orden = i.Orden
    };

    private ProductoDto MapToDto(Producto p, Dictionary<int, Oferta> ofertas)
    {
        var admin = User.EsAdmin();
        var imagenes = p.Imagenes.OrderByDescending(i => i.EsPrincipal).ThenBy(i => i.Orden).Select(MapImagen).ToList();
        ofertas.TryGetValue(p.IdProducto, out var oferta);

        return new ProductoDto
        {
            IdProducto = p.IdProducto,
            Codigo = p.Codigo,
            Nombre = p.Nombre,
            Descripcion = p.Descripcion,
            Cepa = p.Cepa,
            Cosecha = p.Cosecha,
            VolumenMl = p.VolumenMl,
            UnidadesPorCaja = p.UnidadesPorCaja,
            PrecioVenta = p.PrecioVenta,
            PrecioOferta = oferta is null ? null : OfertaService.AplicarDescuento(p.PrecioVenta, oferta.PorcentajeDescuento),
            OfertaPorcentaje = oferta?.PorcentajeDescuento,
            OfertaNombre = oferta?.Nombre,
            PrecioCompra = admin ? p.PrecioCompra : null,
            IdProveedor = admin ? p.IdProveedor : null,
            ProveedorNombre = admin ? p.Proveedor?.Nombre : null,
            Stock = p.Stock,
            StockMinimo = p.StockMinimo,
            IdCategoria = p.IdCategoria,
            CategoriaNombre = p.Categoria?.Nombre,
            IdMarca = p.IdMarca,
            MarcaNombre = p.Marca?.Nombre,
            ImagenPrincipalUrl = imagenes.FirstOrDefault()?.Url,
            Imagenes = imagenes,
            Activo = p.Activo
        };
    }
}
