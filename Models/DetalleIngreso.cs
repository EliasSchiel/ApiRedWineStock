namespace RedWineStock.Models;

public class DetalleIngreso
{
    public int IdDetalleIngreso { get; set; }
    public int IdIngreso { get; set; }
    public int IdProducto { get; set; }
    public int Cantidad { get; set; }                 // unidades (botellas) que entraron al stock
    public int Cajas { get; set; }                    // cuántas de esas llegaron en cajas cerradas (informativo)
    public decimal CostoUnitario { get; set; }

    public decimal Subtotal => Cantidad * CostoUnitario;

    public Ingreso? Ingreso { get; set; }
    public Producto? Producto { get; set; }
}
