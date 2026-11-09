namespace RedWineStock.Dtos;

/// <summary>Un registro de la auditoría, con el nombre de quien hizo la acción.</summary>
public class AuditoriaDto
{
    public int IdAuditoria { get; set; }
    public DateTime Fecha { get; set; }
    public int? IdUsuario { get; set; }
    public string? UsuarioNombre { get; set; }
    public string Accion { get; set; } = string.Empty;    // Alta, Modificacion, Baja, Login, LoginFallido, EnvioReporte
    public string Entidad { get; set; } = string.Empty;   // Producto, Venta...
    public string? IdEntidad { get; set; }
    public string? Detalle { get; set; }                  // JSON con los valores
    public string? Ip { get; set; }
}

/// <summary>Un error registrado (de la API o informado por la app), con el nombre del usuario si se conoce.</summary>
public class LogErrorDto
{
    public int IdLogError { get; set; }
    public DateTime Fecha { get; set; }
    public string Mensaje { get; set; } = string.Empty;
    public string? Excepcion { get; set; }
    public string? Ruta { get; set; }
    public string? MetodoHttp { get; set; }
    public int? IdUsuario { get; set; }
    public string? UsuarioNombre { get; set; }
    public string Origen { get; set; } = string.Empty;    // "API" o "App"
}
