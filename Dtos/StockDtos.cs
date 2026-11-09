using System.ComponentModel.DataAnnotations;

namespace RedWineStock.Dtos;

/// <summary>Corrección manual: se indica cuánto stock hay REALMENTE y por qué cambia (rotura, conteo, regalo...).</summary>
public class AjusteStockDto
{
    [Range(1, int.MaxValue)] public int IdProducto { get; set; }
    [Range(0, 1000000)] public int NuevoStock { get; set; }
    [Required, StringLength(300, MinimumLength = 3)] public string Motivo { get; set; } = string.Empty;
}

public class MovimientoStockDto
{
    public int IdMovimientoStock { get; set; }
    public DateTime Fecha { get; set; }
    public int IdProducto { get; set; }
    public string? ProductoNombre { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public int StockResultante { get; set; }
    public string? Motivo { get; set; }
    public string? UsuarioNombre { get; set; }
    public int? IdVenta { get; set; }
    public int? IdIngreso { get; set; }
}
