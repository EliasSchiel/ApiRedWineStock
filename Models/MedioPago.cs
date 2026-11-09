namespace RedWineStock.Models;

/// <summary>Efectivo, débito, crédito, Mercado Pago, transferencia (se cargan por seed).</summary>
public class MedioPago
{
    public int IdMedioPago { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
}
