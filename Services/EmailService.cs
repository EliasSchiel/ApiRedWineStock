using RedWineStock.Common;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace RedWineStock.Services;

/// <summary>Falla al enviar un mail (SMTP mal configurado, credenciales inválidas, servidor caído, etc.).</summary>
public class EmailException : Exception
{
    public EmailException(string message, Exception? inner = null) : base(message, inner) { }
}

public interface IEmailService
{
    /// <summary>Envía un mail de texto plano con un archivo adjunto. Lanza <see cref="EmailException"/> si falla.</summary>
    Task EnviarConAdjuntoAsync(
        string destinatario, string asunto, string cuerpo,
        string nombreAdjunto, byte[] adjunto, string tipoMime = "application/pdf",
        CancellationToken cancellationToken = default);
}

public class EmailService : IEmailService
{
    private readonly SmtpSettings _smtp;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<SmtpSettings> smtp, ILogger<EmailService> logger)
    {
        _smtp = smtp.Value;
        _logger = logger;
    }

    public async Task EnviarConAdjuntoAsync(
        string destinatario, string asunto, string cuerpo,
        string nombreAdjunto, byte[] adjunto, string tipoMime = "application/pdf",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_smtp.Host) || string.IsNullOrWhiteSpace(_smtp.FromEmail))
            throw new EmailException("El envío de mails no está configurado en el servidor (sección 'Smtp' de appsettings).");

        try
        {
            var mensaje = new MimeMessage();
            mensaje.From.Add(new MailboxAddress(_smtp.FromName, _smtp.FromEmail));
            mensaje.To.Add(MailboxAddress.Parse(destinatario));
            mensaje.Subject = asunto;

            var body = new BodyBuilder { TextBody = cuerpo };
            body.Attachments.Add(nombreAdjunto, adjunto, ContentType.Parse(tipoMime));
            mensaje.Body = body.ToMessageBody();

            using var client = new SmtpClient { Timeout = 20_000 };

            // 465 = SSL directo; 587 (y otros) = STARTTLS
            var seguridad = _smtp.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
            await client.ConnectAsync(_smtp.Host, _smtp.Port, seguridad, cancellationToken);

            if (!string.IsNullOrWhiteSpace(_smtp.User))
                await client.AuthenticateAsync(_smtp.User, _smtp.Password, cancellationToken);

            await client.SendAsync(mensaje, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (AuthenticationException ex)
        {
            _logger.LogError(ex, "El servidor SMTP rechazó las credenciales");
            throw new EmailException("El servidor de correo rechazó las credenciales configuradas. Verificá el usuario y la contraseña de aplicación.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló el envío de mail a {Destinatario}", destinatario);
            throw new EmailException($"No se pudo enviar el mail a través del servidor SMTP ({_smtp.Host}:{_smtp.Port}). Intentá de nuevo más tarde o revisá la configuración.", ex);
        }
    }
}
