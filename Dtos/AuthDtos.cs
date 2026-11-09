using System.ComponentModel.DataAnnotations;

namespace RedWineStock.Dtos;

public class LoginDto
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
}

public class LoginResponseDto
{
    public string Token { get; set; } = string.Empty;
    public DateTime Expira { get; set; }
    public int IdUsuario { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
}

public class CambiarPasswordDto
{
    [Required] public string PasswordActual { get; set; } = string.Empty;
    [Required, StringLength(100, MinimumLength = 6)] public string PasswordNueva { get; set; } = string.Empty;
}
