using RedWineStock.Common;

namespace RedWineStock.Models;

// Sin FK a Usuario a propósito: el registro de auditoría no debe depender
// de que el usuario siga existiendo.
public class Auditoria
{
    public int IdAuditoria { get; set; }
    public DateTime Fecha { get; set; } = Reloj.Ahora;
    public int? IdUsuario { get; set; }
    public string Accion { get; set; } = string.Empty;    // Alta, Modificacion, Baja, Login...
    public string Entidad { get; set; } = string.Empty;   // Producto, Venta...
    public string? IdEntidad { get; set; }
    public string? Detalle { get; set; }                  // JSON con los cambios
    public string? Ip { get; set; }
}
