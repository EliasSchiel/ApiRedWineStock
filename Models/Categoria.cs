namespace RedWineStock.Models;

public class Categoria
{
    public int IdCategoria { get; set; }
    public string Nombre { get; set; } = string.Empty;   // Tinto, Blanco, Espumante, Whisky, Gaseosa...
    public string? Descripcion { get; set; }
    public bool Activo { get; set; } = true;

    public ICollection<Producto> Productos { get; set; } = new List<Producto>();
}
