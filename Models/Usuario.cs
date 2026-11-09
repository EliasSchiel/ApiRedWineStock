using RedWineStock.Common;

namespace RedWineStock.Models;

public class Usuario
{
    public int IdUsuario { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;      // es el "usuario" para iniciar sesión
    public string PasswordHash { get; set; } = string.Empty;
    public int IdRol { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FechaAlta { get; set; } = Reloj.Ahora;

    public Rol? Rol { get; set; }
}
