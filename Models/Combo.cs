using RedWineStock.Common;

namespace RedWineStock.Models;

/// <summary>
/// Paquete de productos con un precio total fijo (ej: Fernet + Coca-Cola = $25.000).
/// Al venderlo se descuenta el stock de cada componente y el precio se reparte entre los renglones.
/// </summary>
public class Combo
{
    public int IdCombo { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public decimal Precio { get; set; }             // precio total del combo
    public bool Activo { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = Reloj.Ahora;

    public ICollection<ComboItem> Items { get; set; } = new List<ComboItem>();
}

/// <summary>Un componente del combo: un producto y cuántas unidades trae.</summary>
public class ComboItem
{
    public int IdComboItem { get; set; }
    public int IdCombo { get; set; }
    public int IdProducto { get; set; }
    public int Cantidad { get; set; } = 1;

    public Combo? Combo { get; set; }
    public Producto? Producto { get; set; }
}
