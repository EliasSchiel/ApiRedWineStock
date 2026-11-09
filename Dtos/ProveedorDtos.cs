using System.ComponentModel.DataAnnotations;

namespace RedWineStock.Dtos;

public class ProveedorDto
{
    public int IdProveedor { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Cuit { get; set; }
    public string? Contacto { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? Direccion { get; set; }
    public bool Activo { get; set; }
    public decimal Deuda { get; set; }   // lo que se le debe por entregas sin pagar
}

public class ProveedorCreateDto
{
    [Required, StringLength(150)] public string Nombre { get; set; } = string.Empty;
    [StringLength(13)] public string? Cuit { get; set; }
    [StringLength(100)] public string? Contacto { get; set; }
    [StringLength(30)] public string? Telefono { get; set; }
    [EmailAddress, StringLength(150)] public string? Email { get; set; }
    [StringLength(200)] public string? Direccion { get; set; }
    public bool Activo { get; set; } = true;
}

public class DeudaProveedorDto
{
    public int IdProveedor { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public int CantidadIngresos { get; set; }
    public decimal Deuda { get; set; }
    public DateTime? ProximoVencimiento { get; set; }   // la fecha límite más cercana entre sus entregas con saldo
    public int EntregasVencidas { get; set; }
}
