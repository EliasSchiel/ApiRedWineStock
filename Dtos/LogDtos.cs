using System.ComponentModel.DataAnnotations;

namespace RedWineStock.Dtos;

/// <summary>Error informado por la app móvil (Blazor Hybrid).</summary>
public class AppLogDto
{
    [Required, StringLength(2000)] public string Mensaje { get; set; } = string.Empty;
    [StringLength(8000)] public string? Detalle { get; set; }       // stack trace / excepción
    [StringLength(300)] public string? Pantalla { get; set; }       // página o pantalla donde ocurrió
    [StringLength(50)] public string? Plataforma { get; set; }      // ej: "Android 14"
    [StringLength(20)] public string? VersionApp { get; set; }      // ej: "1.0.0"
}
