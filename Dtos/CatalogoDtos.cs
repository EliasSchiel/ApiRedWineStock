using System.ComponentModel.DataAnnotations;

namespace RedWineStock.Dtos;

public class CategoriaDto
{
    public int IdCategoria { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; }
}

public class CategoriaCreateDto
{
    [Required, StringLength(80)] public string Nombre { get; set; } = string.Empty;
    [StringLength(300)] public string? Descripcion { get; set; }
    public bool Activo { get; set; } = true;
}

public class MarcaDto
{
    public int IdMarca { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public bool Activo { get; set; }
}

public class MarcaCreateDto
{
    [Required, StringLength(100)] public string Nombre { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
}

public class MedioPagoDto
{
    public int IdMedioPago { get; set; }
    public string Nombre { get; set; } = string.Empty;
}
