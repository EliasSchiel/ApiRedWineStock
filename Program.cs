using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using RedWineStock.Common;
using RedWineStock.Data;
using RedWineStock.Middleware;
using RedWineStock.Services;

// 1. Logger de arranque: registra errores que ocurran antes de que Serilog lea appsettings
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Iniciando RedWine Stock API...");

    var builder = WebApplication.CreateBuilder(args);

    // 2. Serilog reemplaza al logger por defecto y se configura desde appsettings.json ("Serilog")
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    builder.Services.AddControllers()
        .AddJsonOptions(o =>
        {
            // Los enums (EstadoVenta, TipoMovimiento) viajan como texto: "Pendiente", "Cobrada"...
            o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        });

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<AuditoriaService>();
    builder.Services.AddScoped<StockService>();
    builder.Services.AddScoped<OfertaService>();
    builder.Services.AddSingleton<ImagenService>();

    // Reportes PDF (QuestPDF) y envío por mail (MailKit)
    QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("Smtp"));
    builder.Services.AddScoped<ReporteService>();
    builder.Services.AddScoped<IEmailService, EmailService>();

    // Errores no controlados -> tabla LogErrores + respuesta JSON
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("PermitirTodo", policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
    });

    var jwtKey = builder.Configuration["Jwt:Key"]
        ?? throw new InvalidOperationException("Falta 'Jwt:Key' en appsettings.");
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidAudience = builder.Configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
            };
        });
    builder.Services.AddAuthorization();

    var app = builder.Build();

    // 3. Registra cada request: metodo, ruta, codigo de respuesta y duracion
    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} respondio {StatusCode} en {Elapsed:0.0000} ms";
    });

    // Va primero para atrapar errores de todo el pipeline
    app.UseExceptionHandler();

    // Swagger queda activo también en producción: momentáneo para probar la API desde el navegador. 
    app.UseSwagger();
    app.UseSwaggerUI();

    if (!app.Environment.IsDevelopment())
        app.UseHsts();

    app.UseHttpsRedirection();
    app.UseStaticFiles();   // sirve wwwroot/uploads/productos (fotos de los productos)

    app.UseCors("PermitirTodo");
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

    // 4. Migraciones pendientes + administrador inicial
    await DbInicializador.InicializarAsync(app.Services, app.Configuration, app.Logger);

    app.Run();
}
// HostAbortedException: la lanzan a proposito herramientas como "dotnet ef" para frenar la app; no es un error
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "La aplicacion termino de forma inesperada");
    throw;
}
finally
{
    Log.CloseAndFlush(); // vuelca a disco lo que quede en el buffer antes de apagar
}
