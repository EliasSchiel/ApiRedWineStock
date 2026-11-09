using RedWineStock.Common;

namespace RedWineStock.Models;

/// <summary>Entrega/compra a un proveedor. Suma stock y, si no se pagó todo, genera una deuda con el proveedor.</summary>
public class Ingreso
{
    public int IdIngreso { get; set; }
    public DateTime Fecha { get; set; } = Reloj.Ahora;
    public int IdProveedor { get; set; }
    public int IdUsuario { get; set; }
    public string? NumeroComprobante { get; set; }   // número de remito/factura del proveedor
    public string? Observaciones { get; set; }
    public decimal Total { get; set; }
    public decimal MontoPagado { get; set; }          // suma de PagosProveedor
    public DateTime? FechaVencimiento { get; set; }   // hasta cuándo se acordó pagar la entrega (solo fecha)

    public decimal Saldo => Total - MontoPagado;

    public Proveedor? Proveedor { get; set; }
    public Usuario? Usuario { get; set; }
    public ICollection<DetalleIngreso> Detalles { get; set; } = new List<DetalleIngreso>();
    public ICollection<PagoProveedor> Pagos { get; set; } = new List<PagoProveedor>();
}
