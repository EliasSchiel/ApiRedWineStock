using RedWineStock.Common;

namespace RedWineStock.Models;

public class LogError
{
    public int IdLogError { get; set; }
    public DateTime Fecha { get; set; } = Reloj.Ahora;
    public string Mensaje { get; set; } = string.Empty;
    public string? Excepcion { get; set; }    // tipo + stack trace
    public string? Ruta { get; set; }
    public string? MetodoHttp { get; set; }
    public int? IdUsuario { get; set; }
    public string Origen { get; set; } = OrigenLog.Api;   // "API" o "App"
}
