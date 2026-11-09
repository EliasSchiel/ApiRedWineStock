namespace RedWineStock.Common;

/// <summary>
/// Hora "de la vinoteca" (Argentina). El hosting puede tener otra zona horaria y las ventas
/// "de hoy" tienen que contarse con el día argentino, así que toda fecha se toma de acá.
/// </summary>
public static class Reloj
{
    private static readonly TimeZoneInfo? Zona = BuscarZona();

    public static DateTime Ahora => Zona is null
        ? DateTime.Now
        : TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zona);

    public static DateTime Hoy => Ahora.Date;

    private static TimeZoneInfo? BuscarZona()
    {
        // Windows usa "Argentina Standard Time"; Linux/macOS usan el id IANA
        foreach (var id in new[] { "Argentina Standard Time", "America/Argentina/Buenos_Aires" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return null;
    }
}
