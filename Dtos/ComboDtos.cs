using System.ComponentModel.DataAnnotations;

namespace RedWineStock.Dtos;

public class ComboItemDto
{
    public int IdProducto { get; set; }
    public string? ProductoNombre { get; set; }
    public string? ProductoMarca { get; set; }
    public int? ProductoCosecha { get; set; }
    public int ProductoStock { get; set; }
    public decimal PrecioVenta { get; set; }   // precio de lista del producto suelto
    public int Cantidad { get; set; }
}

public class ComboDto
{
    public int IdCombo { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public decimal Precio { get; set; }
    public decimal PrecioLista { get; set; }   // lo que costaría comprar todo suelto
    public decimal Ahorro { get; set; }        // PrecioLista - Precio (puede ser negativo si el combo es más caro)
    public bool Activo { get; set; }
    public int Disponible { get; set; }        // cuántos combos se pueden armar con el stock actual
    public List<ComboItemDto> Items { get; set; } = new();
}

public class ComboItemCreateDto
{
    [Range(1, int.MaxValue)] public int IdProducto { get; set; }
    [Range(1, 999)] public int Cantidad { get; set; } = 1;
}

public class ComboCreateDto
{
    [Required, StringLength(100)] public string Nombre { get; set; } = string.Empty;
    [StringLength(300)] public string? Descripcion { get; set; }
    [Range(0.01, 99999999)] public decimal Precio { get; set; }
    [Required, MinLength(1)] public List<ComboItemCreateDto> Items { get; set; } = new();
}

public class ComboUpdateDto : ComboCreateDto
{
    public bool Activo { get; set; } = true;
}
