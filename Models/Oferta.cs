namespace RedWineStock.Models;

/// <summary>Descuento porcentual sobre un producto durante un rango de fechas. Se aplica solo al vender.</summary>
public class Oferta
{
    public int IdOferta { get; set; }
    public int IdProducto { get; set; }
    public string Nombre { get; set; } = string.Empty;       // ej: "Promo Malbec de invierno"
    public decimal PorcentajeDescuento { get; set; }          // 1 a 90
    public DateTime FechaDesde { get; set; }
    public DateTime FechaHasta { get; set; }
    public bool Activa { get; set; } = true;

    public Producto? Producto { get; set; }

    public bool EstaVigente(DateTime hoy) => Activa && FechaDesde.Date <= hoy.Date && hoy.Date <= FechaHasta.Date;
}
