namespace RedWineStock.Common;

/// <summary>Sección "Smtp" de appsettings.json (los valores reales NO se suben a git).</summary>
public class SmtpSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string User { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromName { get; set; } = "RedWine Stock";
    public string FromEmail { get; set; } = string.Empty;
}
