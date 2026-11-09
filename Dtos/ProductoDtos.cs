using System.ComponentModel.DataAnnotations;

namespace RedWineStock.Dtos;

public class ProductoDto
{
    public int IdProducto { get; set; }
    public string? Codigo { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string? Cepa { get; set; }
    public int? Cosecha { get; set; }
    public int VolumenMl { get; set; }
    public int UnidadesPorCaja { get; set; }

    public decimal PrecioVenta { get; set; }
    // Si hay una oferta vigente hoy: el precio final, el % y el nombre de la oferta
    public decimal? PrecioOferta { get; set; }
    public decimal? OfertaPorcentaje { get; set; }
    public string? OfertaNombre { get; set; }
    public decimal PrecioFinal => PrecioOferta ?? PrecioVenta;

    // SOLO para Admin (para el Operador vienen en null)
    public decimal? PrecioCompra { get; set; }
    public int? IdProveedor { get; set; }
    public string? ProveedorNombre { get; set; }

    public int Stock { get; set; }
    public int StockMinimo { get; set; }
    public bool StockBajo => Stock <= StockMinimo;

    public int IdCategoria { get; set; }
    public string? CategoriaNombre { get; set; }
    public int? IdMarca { get; set; }
    public string? MarcaNombre { get; set; }

    public string? ImagenPrincipalUrl { get; set; }
    public List<ProductoImagenDto> Imagenes { get; set; } = new();
    public bool Activo { get; set; }
}

public class ProductoImagenDto
{
    public int IdProductoImagen { get; set; }
    public string Url { get; set; } = string.Empty;
    public bool EsPrincipal { get; set; }
    public int Orden { get; set; }
}

// El stock NO se carga acá: entra con un Ingreso, sale con una Venta y se corrige con un Ajuste,
// así cada movimiento queda registrado en el historial.
public class ProductoCreateDto
{
    [StringLength(50)] public string? Codigo { get; set; }
    [Required, StringLength(150)] public string Nombre { get; set; } = string.Empty;
    [StringLength(1000)] public string? Descripcion { get; set; }
    [StringLength(80)] public string? Cepa { get; set; }
    [Range(1900, 2100)] public int? Cosecha { get; set; }
    [Range(1, 20000)] public int VolumenMl { get; set; } = 750;
    [Range(1, 1000)] public int UnidadesPorCaja { get; set; } = 6;     // botellas por caja del proveedor
    [Range(0, 99999999)] public decimal PrecioCompra { get; set; }     // lo ignora la API si quien guarda es Operador
    [Range(0.01, 99999999)] public decimal PrecioVenta { get; set; }
    [Range(0, int.MaxValue)] public int StockMinimo { get; set; }
    [Range(1, int.MaxValue)] public int IdCategoria { get; set; }
    public int? IdMarca { get; set; }
    public int? IdProveedor { get; set; }                               // lo ignora la API si quien guarda es Operador
}

public class ProductoUpdateDto : ProductoCreateDto
{
    public bool Activo { get; set; } = true;
}
