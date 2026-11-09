using System.ComponentModel.DataAnnotations;

namespace RedWineStock.Dtos;

public class OfertaDto
{
    public int IdOferta { get; set; }
    public int IdProducto { get; set; }
    public string? ProductoNombre { get; set; }
    public string? ProductoMarca { get; set; }
    public int? ProductoCosecha { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public decimal PorcentajeDescuento { get; set; }
    public decimal PrecioConOferta { get; set; }
    public DateTime FechaDesde { get; set; }
    public DateTime FechaHasta { get; set; }
    public bool Activa { get; set; }
    public bool Vigente { get; set; }   // activa y hoy está dentro del rango de fechas
}

public class OfertaCreateDto
{
    [Range(1, int.MaxValue)] public int IdProducto { get; set; }
    [Required, StringLength(100)] public string Nombre { get; set; } = string.Empty;
    [Range(1, 90)] public decimal PorcentajeDescuento { get; set; }
    public DateTime FechaDesde { get; set; }
    public DateTime FechaHasta { get; set; }
}

public class OfertaUpdateDto : OfertaCreateDto
{
    public bool Activa { get; set; } = true;
}
