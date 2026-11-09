using System.ComponentModel.DataAnnotations;

namespace RedWineStock.Dtos;

public class UsuarioDto
{
    public int IdUsuario { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public bool Activo { get; set; }
    public DateTime FechaAlta { get; set; }
}

public class UsuarioCreateDto
{
    [Required, StringLength(100, MinimumLength = 2)] public string Nombre { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(150)] public string Email { get; set; } = string.Empty;
    [Required, StringLength(100, MinimumLength = 6)] public string Password { get; set; } = string.Empty;
    [Required] public string Rol { get; set; } = "Operador";   // Admin | Operador
}

public class UsuarioUpdateDto
{
    [Required, StringLength(100, MinimumLength = 2)] public string Nombre { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(150)] public string Email { get; set; } = string.Empty;
    [Required] public string Rol { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
}

/// <summary>El Admin define una contraseña nueva para un usuario que se olvidó la suya.</summary>
public class ResetPasswordDto
{
    [Required, StringLength(100, MinimumLength = 6)] public string PasswordNueva { get; set; } = string.Empty;
}

public class RolDto
{
    public int IdRol { get; set; }
    public string Nombre { get; set; } = string.Empty;
}
