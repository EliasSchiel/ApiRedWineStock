using RedWineStock.Common;

namespace RedWineStock.Models;

/// <summary>Cliente conocido de la vinoteca. En una venta el cliente es opcional (salvo que quede pendiente de pago).</summary>
public class Cliente
{
    public int IdCliente { get; set; }
    public string Nombre { get; set; } = string.Empty;   // nombre completo, como lo anotaría en su planilla
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? Direccion { get; set; }
    public string? Notas { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FechaAlta { get; set; } = Reloj.Ahora;

    public ICollection<Venta> Ventas { get; set; } = new List<Venta>();
}
