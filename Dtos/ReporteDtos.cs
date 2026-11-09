using System.ComponentModel.DataAnnotations;
using RedWineStock.Models;

namespace RedWineStock.Dtos;

public static class ReporteTipos
{
    public const string Stock = "stock";
    public const string StockBajo = "stock-bajo";
    public const string Ventas = "ventas";
    public const string MasVendidos = "mas-vendidos";
    public const string Deudores = "deudores";
    public const string Ingresos = "ingresos";                       // solo Admin (muestra costos)
    public const string DeudasProveedores = "deudas-proveedores";    // solo Admin

    public static readonly string[] Todos = { Stock, StockBajo, Ventas, MasVendidos, Deudores, Ingresos, DeudasProveedores };
    public static readonly string[] SoloAdmin = { Ingresos, DeudasProveedores };
}

/// <summary>Filtros que aplican a los distintos reportes (cada reporte usa solo los que le corresponden).</summary>
public record ReporteFiltros(
    DateTime? Desde = null,
    DateTime? Hasta = null,
    int? IdCategoria = null,
    int? IdProveedor = null,
    EstadoVenta? Estado = null);

/// <summary>PDF ya generado, listo para descargar o adjuntar a un mail.</summary>
public record ReporteArchivo(byte[] Contenido, string NombreArchivo, string Titulo);

public class EnviarReporteDto
{
    [Required, EmailAddress, StringLength(150)] public string Email { get; set; } = string.Empty;

    // Filtros opcionales (los mismos que los GET de cada reporte)
    public DateTime? Desde { get; set; }
    public DateTime? Hasta { get; set; }
    [Range(1, int.MaxValue)] public int? IdCategoria { get; set; }
    [Range(1, int.MaxValue)] public int? IdProveedor { get; set; }
    public EstadoVenta? Estado { get; set; }
}
