using RedWineStock.Common;

namespace RedWineStock.Models;

public class Proveedor
{
    public int IdProveedor { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Cuit { get; set; }
    public string? Contacto { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? Direccion { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FechaAlta { get; set; } = Reloj.Ahora;

    public ICollection<Ingreso> Ingresos { get; set; } = new List<Ingreso>();
    public ICollection<Producto> Productos { get; set; } = new List<Producto>();
}
