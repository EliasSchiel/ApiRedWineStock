using System.ComponentModel.DataAnnotations;

namespace RedWineStock.Dtos;

public class ClienteDto
{
    public int IdCliente { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? Direccion { get; set; }
    public string? Notas { get; set; }
    public bool Activo { get; set; }
    public decimal Deuda { get; set; }   // suma de saldos de sus ventas pendientes
}

public class ClienteCreateDto
{
    [Required, StringLength(150)] public string Nombre { get; set; } = string.Empty;
    [StringLength(30)] public string? Telefono { get; set; }
    [EmailAddress, StringLength(150)] public string? Email { get; set; }
    [StringLength(200)] public string? Direccion { get; set; }
    [StringLength(500)] public string? Notas { get; set; }
    public bool Activo { get; set; } = true;
}

/// <summary>"Cuenta corriente" simple: lo que el cliente debe y qué ventas lo componen.</summary>
public class ClienteCuentaDto
{
    public ClienteDto Cliente { get; set; } = new();
    public decimal Deuda { get; set; }
    public List<VentaResumenDto> VentasPendientes { get; set; } = new();
}

public class DeudorDto
{
    public int IdCliente { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public int CantidadVentas { get; set; }
    public decimal Deuda { get; set; }
    public DateTime VentaMasAntigua { get; set; }
}
