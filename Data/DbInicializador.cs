using Microsoft.EntityFrameworkCore;
using RedWineStock.Common;
using RedWineStock.Models;

namespace RedWineStock.Data;

/// <summary>
/// Se ejecuta al arrancar la API: aplica las migraciones pendientes (así el despliegue no
/// necesita correr Update-Database a mano) y crea el administrador inicial si no hay usuarios.
/// </summary>
public static class DbInicializador
{
    public static async Task InicializarAsync(IServiceProvider services, IConfiguration config, ILogger logger)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (config.GetValue("Database:MigrarAlIniciar", true))
        {
            logger.LogInformation("Aplicando migraciones pendientes...");
            await db.Database.MigrateAsync();
        }

        if (!await db.Usuarios.AnyAsync())
        {
            var email = (config["AdminInicial:Email"] ?? "admin@redwine.com").Trim().ToLowerInvariant();
            var password = config["AdminInicial:Password"];
            if (string.IsNullOrWhiteSpace(password) || password.StartsWith("CAMBIAR"))
            {
                logger.LogWarning("No se creó el administrador inicial: falta 'AdminInicial:Password' en appsettings.");
                return;
            }

            db.Usuarios.Add(new Usuario
            {
                Nombre = config["AdminInicial:Nombre"] ?? "Administrador",
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                IdRol = Roles.IdAdmin,
                Activo = true,
                FechaAlta = Reloj.Ahora
            });
            await db.SaveChangesAsync();
            logger.LogInformation("Administrador inicial creado: {Email}", email);
        }
    }
}
