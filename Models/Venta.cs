using RedWineStock.Common;

namespace RedWineStock.Models;

public class Venta
{
    public int IdVenta { get; set; }
    public DateTime Fecha { get; set; } = Reloj.Ahora;
    public int? IdCliente { get; set; }          // opcional: solo si el cliente es conocido (obligatorio si queda saldo)
    public int IdUsuario { get; set; }           // quién la registró
    public decimal Subtotal { get; set; }        // suma de los renglones (ya con ofertas)
    public decimal Descuento { get; set; }       // descuento final en pesos (ej: 82.000 -> 80.000 = 2.000)
    public decimal Total { get; set; }           // Subtotal - Descuento
    public decimal MontoCobrado { get; set; }    // suma de Cobros (se mantiene al día en cada cobro)
    public EstadoVenta Estado { get; set; } = EstadoVenta.Cobrada;
    public string? Observaciones { get; set; }
    public DateTime? FechaAnulacion { get; set; }
    public string? MotivoAnulacion { get; set; }
    public int? IdUsuarioAnulacion { get; set; }

    public decimal Saldo => Estado == EstadoVenta.Anulada ? 0 : Total - MontoCobrado;

    public Cliente? Cliente { get; set; }
    public Usuario? Usuario { get; set; }
    public ICollection<DetalleVenta> Detalles { get; set; } = new List<DetalleVenta>();
    public ICollection<Cobro> Cobros { get; set; } = new List<Cobro>();
}
