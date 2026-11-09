using RedWineStock.Common;

namespace RedWineStock.Models;

/// <summary>
/// Historial de stock: cada cambio de stock deja una fila (quién, cuándo, cuánto y por qué).
/// Cantidad va con signo: +12 entra mercadería, -2 sale por venta.
/// </summary>
public class MovimientoStock
{
    public int IdMovimientoStock { get; set; }
    public DateTime Fecha { get; set; } = Reloj.Ahora;
    public int IdProducto { get; set; }
    public TipoMovimiento Tipo { get; set; }
    public int Cantidad { get; set; }
    public int StockResultante { get; set; }
    public string? Motivo { get; set; }
    public int IdUsuario { get; set; }
    public int? IdVenta { get; set; }
    public int? IdIngreso { get; set; }

    public Producto? Producto { get; set; }
    public Usuario? Usuario { get; set; }
    public Venta? Venta { get; set; }
    public Ingreso? Ingreso { get; set; }
}
