using System.ComponentModel.DataAnnotations;
using RedWineStock.Common;

namespace RedWineStock.Dtos;

public class IngresoItemCreateDto
{
    [Range(1, int.MaxValue)] public int IdProducto { get; set; }
    // Lo que llegó = Cajas × (botellas por caja del producto) + Cantidad (botellas sueltas)
    [Range(0, 100000)] public int Cajas { get; set; }
    [Range(0, 1000000)] public int Cantidad { get; set; }
    // Costo de UNA botella (si el proveedor cotiza por caja: precio de la caja ÷ botellas por caja)
    [Range(0, 99999999)] public decimal CostoUnitario { get; set; }
}

public class PagoProveedorCreateDto
{
    [Range(1, int.MaxValue)] public int IdMedioPago { get; set; }
    [Range(0.01, 99999999)] public decimal Monto { get; set; }
    [StringLength(300)] public string? Observaciones { get; set; }
}

public class IngresoVencimientoDto
{
    public DateTime? FechaVencimiento { get; set; }   // null = quitar el vencimiento
}

public class IngresoCreateDto
{
    [Range(1, int.MaxValue)] public int IdProveedor { get; set; }
    [StringLength(30)] public string? NumeroComprobante { get; set; }
    [StringLength(500)] public string? Observaciones { get; set; }
    // Hasta cuándo se acordó pagar lo que quede debiendo (opcional)
    public DateTime? FechaVencimiento { get; set; }
    [Required, MinLength(1)] public List<IngresoItemCreateDto> Items { get; set; } = new();

    // Lo que se le paga al proveedor en el momento. Vacío = queda todo como deuda.
    public List<PagoProveedorCreateDto> Pagos { get; set; } = new();
}

public class IngresoDto
{
    public int IdIngreso { get; set; }
    public DateTime Fecha { get; set; }
    public int IdProveedor { get; set; }
    public string? ProveedorNombre { get; set; }
    public string? UsuarioNombre { get; set; }
    public string? NumeroComprobante { get; set; }
    public string? Observaciones { get; set; }
    public decimal Total { get; set; }
    public decimal MontoPagado { get; set; }
    public decimal Saldo { get; set; }
    public DateTime? FechaVencimiento { get; set; }
    public bool Vencido => Saldo > 0 && FechaVencimiento.HasValue && FechaVencimiento.Value.Date < Reloj.Hoy;
    public List<DetalleIngresoDto> Detalles { get; set; } = new();
    public List<PagoProveedorDto> Pagos { get; set; } = new();
}

public class DetalleIngresoDto
{
    public int IdProducto { get; set; }
    public string? ProductoNombre { get; set; }
    public string? ProductoMarca { get; set; }
    public int? ProductoCosecha { get; set; }
    public int Cajas { get; set; }
    public int Cantidad { get; set; }
    public decimal CostoUnitario { get; set; }
    public decimal Subtotal => Cantidad * CostoUnitario;
}

public class PagoProveedorDto
{
    public int IdPagoProveedor { get; set; }
    public DateTime Fecha { get; set; }
    public int IdMedioPago { get; set; }
    public string? MedioPago { get; set; }
    public decimal Monto { get; set; }
    public string? UsuarioNombre { get; set; }
    public string? Observaciones { get; set; }
}
