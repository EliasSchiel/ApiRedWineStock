namespace RedWineStock.Models;

/// <summary>Renglón de una venta. Guarda "fotos" de los precios del momento: si mañana cambian, la venta no se altera.</summary>
public class DetalleVenta
{
    public int IdDetalleVenta { get; set; }
    public int IdVenta { get; set; }
    public int IdProducto { get; set; }
    public int Cantidad { get; set; }
    public decimal PrecioLista { get; set; }        // precio de lista al vender
    public decimal PorcentajeOferta { get; set; }   // 0 si no había oferta vigente
    public int? IdOferta { get; set; }
    public int? IdCombo { get; set; }               // si el renglón salió de un combo (el precio es la parte del combo)
    public decimal PrecioUnitario { get; set; }     // precio final cobrado (lista menos oferta)
    public decimal CostoUnitario { get; set; }      // costo al vender (para calcular la ganancia)

    public decimal Subtotal => Cantidad * PrecioUnitario;

    public Venta? Venta { get; set; }
    public Producto? Producto { get; set; }
    public Oferta? Oferta { get; set; }
    public Combo? Combo { get; set; }
}
