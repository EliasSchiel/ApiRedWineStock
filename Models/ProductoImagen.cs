using RedWineStock.Common;

namespace RedWineStock.Models;

public class ProductoImagen
{
    public int IdProductoImagen { get; set; }
    public int IdProducto { get; set; }
    // Solo se guarda el nombre del archivo; la URL completa se arma en la API
    // con el host actual (así no queda atada a localhost ni a un dominio).
    public string NombreArchivo { get; set; } = string.Empty;
    public bool EsPrincipal { get; set; }
    public int Orden { get; set; }
    public DateTime FechaSubida { get; set; } = Reloj.Ahora;

    public Producto? Producto { get; set; }
}
