using RedWineStock.Common;

namespace RedWineStock.Models;

/// <summary>Pago que hace la vinoteca a un proveedor por un ingreso (la "deuda de entregas" de su planilla).</summary>
public class PagoProveedor
{
    public int IdPagoProveedor { get; set; }
    public int IdIngreso { get; set; }
    public int IdMedioPago { get; set; }
    public decimal Monto { get; set; }
    public DateTime Fecha { get; set; } = Reloj.Ahora;
    public int IdUsuario { get; set; }
    public string? Observaciones { get; set; }

    public Ingreso? Ingreso { get; set; }
    public MedioPago? MedioPago { get; set; }
    public Usuario? Usuario { get; set; }
}
