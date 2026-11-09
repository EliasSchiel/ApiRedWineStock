using RedWineStock.Common;

namespace RedWineStock.Models;

/// <summary>Un pago recibido de una venta. Una venta puede tener varios (cobro parcial, "me paga el resto el viernes").</summary>
public class Cobro
{
    public int IdCobro { get; set; }
    public int IdVenta { get; set; }
    public int IdMedioPago { get; set; }
    public decimal Monto { get; set; }
    public DateTime Fecha { get; set; } = Reloj.Ahora;
    public int IdUsuario { get; set; }
    public string? Observaciones { get; set; }

    public Venta? Venta { get; set; }
    public MedioPago? MedioPago { get; set; }
    public Usuario? Usuario { get; set; }
}
