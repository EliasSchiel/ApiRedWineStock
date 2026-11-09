using System.ComponentModel.DataAnnotations;

namespace RedWineStock.Dtos;

// ---- Lo que manda la app al confirmar la venta ----
public class VentaItemCreateDto
{
    [Range(1, int.MaxValue)] public int IdProducto { get; set; }
    [Range(1, int.MaxValue)] public int Cantidad { get; set; }
}

public class VentaComboCreateDto
{
    [Range(1, int.MaxValue)] public int IdCombo { get; set; }
    [Range(1, int.MaxValue)] public int Cantidad { get; set; }
}

public class CobroCreateDto
{
    [Range(1, int.MaxValue)] public int IdMedioPago { get; set; }
    [Range(0.01, 99999999)] public decimal Monto { get; set; }
    [StringLength(300)] public string? Observaciones { get; set; }
}

public class VentaCreateDto
{
    // Opcional: solo si el cliente es conocido. Obligatorio si la venta queda con saldo pendiente.
    public int? IdCliente { get; set; }

    // Descuento final en pesos sobre el total (ej: la compra suma 82.000 y se cobra 80.000 => 2.000)
    [Range(0, 99999999)] public decimal Descuento { get; set; }

    [StringLength(500)] public string? Observaciones { get; set; }

    // Productos sueltos. Puede venir vacía si la venta es solo de combos (se valida en el controlador).
    public List<VentaItemCreateDto> Items { get; set; } = new();

    // Combos vendidos (cada uno suma todos sus productos al precio fijo del combo).
    public List<VentaComboCreateDto> Combos { get; set; } = new();

    // Lo que se cobra en el momento. Vacío = "a pagar más tarde" (venta pendiente, requiere cliente).
    // Puede haber varios (ej: parte en efectivo y parte con transferencia).
    public List<CobroCreateDto> Cobros { get; set; } = new();
}

public class AnularVentaDto
{
    [Required, StringLength(300, MinimumLength = 3)] public string Motivo { get; set; } = string.Empty;
}

// ---- Respuestas ----
public class VentaResumenDto
{
    public int IdVenta { get; set; }
    public DateTime Fecha { get; set; }
    public int? IdCliente { get; set; }
    public string? ClienteNombre { get; set; }
    public string? UsuarioNombre { get; set; }
    public decimal Total { get; set; }
    public decimal MontoCobrado { get; set; }
    public decimal Saldo { get; set; }
    public string Estado { get; set; } = string.Empty;
    public int CantidadItems { get; set; }
}

public class VentaDto
{
    public int IdVenta { get; set; }
    public DateTime Fecha { get; set; }
    public int? IdCliente { get; set; }
    public string? ClienteNombre { get; set; }
    public string? UsuarioNombre { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Descuento { get; set; }
    public decimal Total { get; set; }
    public decimal MontoCobrado { get; set; }
    public decimal Saldo { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string? Observaciones { get; set; }
    public DateTime? FechaAnulacion { get; set; }
    public string? MotivoAnulacion { get; set; }
    public List<DetalleVentaDto> Detalles { get; set; } = new();
    public List<CobroDto> Cobros { get; set; } = new();
}

public class DetalleVentaDto
{
    public int IdProducto { get; set; }
    public string? ProductoNombre { get; set; }
    public string? ProductoMarca { get; set; }
    public int? ProductoCosecha { get; set; }
    public int? IdCombo { get; set; }          // si el renglón salió de un combo
    public string? ComboNombre { get; set; }
    public int Cantidad { get; set; }
    public decimal PrecioLista { get; set; }
    public decimal PorcentajeOferta { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Subtotal => Cantidad * PrecioUnitario;
}

public class CobroDto
{
    public int IdCobro { get; set; }
    public DateTime Fecha { get; set; }
    public int IdMedioPago { get; set; }
    public string? MedioPago { get; set; }
    public decimal Monto { get; set; }
    public string? UsuarioNombre { get; set; }
    public string? Observaciones { get; set; }
}
