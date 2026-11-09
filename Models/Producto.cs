using RedWineStock.Common;

namespace RedWineStock.Models;

public class Producto
{
    public int IdProducto { get; set; }
    public string? Codigo { get; set; }             // código de barras / interno (opcional, único)
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string? Cepa { get; set; }               // Malbec, Cabernet...
    public int? Cosecha { get; set; }               // año
    public int VolumenMl { get; set; } = 750;
    public int UnidadesPorCaja { get; set; } = 6;   // botellas que trae una caja del proveedor
    public decimal PrecioCompra { get; set; }       // último costo (solo lo ve el Admin)
    public decimal PrecioVenta { get; set; }        // precio de lista
    public int Stock { get; set; }
    public int StockMinimo { get; set; }
    public int IdCategoria { get; set; }
    public int? IdMarca { get; set; }
    public int? IdProveedor { get; set; }           // proveedor habitual (opcional)
    public bool Activo { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = Reloj.Ahora;

    public Categoria? Categoria { get; set; }
    public Marca? Marca { get; set; }
    public Proveedor? Proveedor { get; set; }
    public ICollection<ProductoImagen> Imagenes { get; set; } = new List<ProductoImagen>();
    public ICollection<Oferta> Ofertas { get; set; } = new List<Oferta>();
    public ICollection<DetalleVenta> DetallesVenta { get; set; } = new List<DetalleVenta>();
    public ICollection<DetalleIngreso> DetallesIngreso { get; set; } = new List<DetalleIngreso>();
    public ICollection<MovimientoStock> Movimientos { get; set; } = new List<MovimientoStock>();
}
