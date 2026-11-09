using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using RedWineStock.Common;
using RedWineStock.Models;

namespace RedWineStock.Data;

public class AppDbContext : DbContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AppDbContext(DbContextOptions<AppDbContext> options, IHttpContextAccessor httpContextAccessor)
        : base(options)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public DbSet<Rol> Roles => Set<Rol>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Proveedor> Proveedores => Set<Proveedor>();
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Marca> Marcas => Set<Marca>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<ProductoImagen> ProductoImagenes => Set<ProductoImagen>();
    public DbSet<MedioPago> MediosPago => Set<MedioPago>();
    public DbSet<Oferta> Ofertas => Set<Oferta>();
    public DbSet<Combo> Combos => Set<Combo>();
    public DbSet<ComboItem> ComboItems => Set<ComboItem>();
    public DbSet<Venta> Ventas => Set<Venta>();
    public DbSet<DetalleVenta> DetallesVenta => Set<DetalleVenta>();
    public DbSet<Cobro> Cobros => Set<Cobro>();
    public DbSet<MovimientoStock> MovimientosStock => Set<MovimientoStock>();
    public DbSet<Ingreso> Ingresos => Set<Ingreso>();
    public DbSet<DetalleIngreso> DetallesIngreso => Set<DetalleIngreso>();
    public DbSet<PagoProveedor> PagosProveedor => Set<PagoProveedor>();
    public DbSet<Auditoria> Auditorias => Set<Auditoria>();
    public DbSet<LogError> LogErrores => Set<LogError>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ---------- Seguridad ----------
        modelBuilder.Entity<Rol>(e =>
        {
            e.HasKey(r => r.IdRol);
            e.Property(r => r.Nombre).HasMaxLength(30).IsRequired();
            e.HasIndex(r => r.Nombre).IsUnique();
        });

        modelBuilder.Entity<Usuario>(e =>
        {
            e.HasKey(u => u.IdUsuario);
            e.Property(u => u.Nombre).HasMaxLength(100).IsRequired();
            e.Property(u => u.Email).HasMaxLength(150).IsRequired();
            e.Property(u => u.PasswordHash).HasMaxLength(100).IsRequired();
            e.HasIndex(u => u.Email).IsUnique();
            e.HasOne(u => u.Rol).WithMany(r => r.Usuarios).HasForeignKey(u => u.IdRol);
        });

        // ---------- Personas ----------
        modelBuilder.Entity<Cliente>(e =>
        {
            e.HasKey(c => c.IdCliente);
            e.Property(c => c.Nombre).HasMaxLength(150).IsRequired();
            e.Property(c => c.Telefono).HasMaxLength(30);
            e.Property(c => c.Email).HasMaxLength(150);
            e.Property(c => c.Direccion).HasMaxLength(200);
            e.Property(c => c.Notas).HasMaxLength(500);
            e.HasIndex(c => c.Nombre);
        });

        modelBuilder.Entity<Proveedor>(e =>
        {
            e.HasKey(p => p.IdProveedor);
            e.Property(p => p.Nombre).HasMaxLength(150).IsRequired();
            e.Property(p => p.Cuit).HasMaxLength(13);
            e.Property(p => p.Contacto).HasMaxLength(100);
            e.Property(p => p.Telefono).HasMaxLength(30);
            e.Property(p => p.Email).HasMaxLength(150);
            e.Property(p => p.Direccion).HasMaxLength(200);
            e.HasIndex(p => p.Cuit).IsUnique().HasFilter("[Cuit] IS NOT NULL");
        });

        // ---------- Catálogo ----------
        modelBuilder.Entity<Categoria>(e =>
        {
            e.HasKey(c => c.IdCategoria);
            e.Property(c => c.Nombre).HasMaxLength(80).IsRequired();
            e.Property(c => c.Descripcion).HasMaxLength(300);
            e.HasIndex(c => c.Nombre).IsUnique();
        });

        modelBuilder.Entity<Marca>(e =>
        {
            e.HasKey(m => m.IdMarca);
            e.Property(m => m.Nombre).HasMaxLength(100).IsRequired();
            e.HasIndex(m => m.Nombre).IsUnique();
        });

        modelBuilder.Entity<Producto>(e =>
        {
            e.HasKey(p => p.IdProducto);
            e.Property(p => p.Codigo).HasMaxLength(50);
            e.Property(p => p.Nombre).HasMaxLength(150).IsRequired();
            e.Property(p => p.Descripcion).HasMaxLength(1000);
            e.Property(p => p.Cepa).HasMaxLength(80);
            e.Property(p => p.UnidadesPorCaja).HasDefaultValue(6);
            e.Property(p => p.PrecioCompra).HasPrecision(12, 2);
            e.Property(p => p.PrecioVenta).HasPrecision(12, 2);
            e.HasIndex(p => p.Codigo).IsUnique().HasFilter("[Codigo] IS NOT NULL");
            e.HasIndex(p => p.Nombre);
            e.HasOne(p => p.Categoria).WithMany(c => c.Productos).HasForeignKey(p => p.IdCategoria);
            e.HasOne(p => p.Marca).WithMany(m => m.Productos).HasForeignKey(p => p.IdMarca);
            e.HasOne(p => p.Proveedor).WithMany(pr => pr.Productos).HasForeignKey(p => p.IdProveedor);
        });

        modelBuilder.Entity<ProductoImagen>(e =>
        {
            e.HasKey(i => i.IdProductoImagen);
            e.Property(i => i.NombreArchivo).HasMaxLength(100).IsRequired();
            e.HasOne(i => i.Producto).WithMany(p => p.Imagenes)
             .HasForeignKey(i => i.IdProducto).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MedioPago>(e =>
        {
            e.HasKey(m => m.IdMedioPago);
            e.Property(m => m.Nombre).HasMaxLength(50).IsRequired();
            e.HasIndex(m => m.Nombre).IsUnique();
        });

        modelBuilder.Entity<Oferta>(e =>
        {
            e.HasKey(o => o.IdOferta);
            e.Property(o => o.Nombre).HasMaxLength(100).IsRequired();
            e.Property(o => o.PorcentajeDescuento).HasPrecision(5, 2);
            e.Property(o => o.FechaDesde).HasColumnType("date");
            e.Property(o => o.FechaHasta).HasColumnType("date");
            e.HasIndex(o => new { o.IdProducto, o.FechaDesde, o.FechaHasta });
            e.HasOne(o => o.Producto).WithMany(p => p.Ofertas).HasForeignKey(o => o.IdProducto);
        });

        modelBuilder.Entity<Combo>(e =>
        {
            e.HasKey(c => c.IdCombo);
            e.Property(c => c.Nombre).HasMaxLength(100).IsRequired();
            e.Property(c => c.Descripcion).HasMaxLength(300);
            e.Property(c => c.Precio).HasPrecision(12, 2);
        });

        modelBuilder.Entity<ComboItem>(e =>
        {
            e.HasKey(i => i.IdComboItem);
            e.HasIndex(i => new { i.IdCombo, i.IdProducto }).IsUnique();
            e.HasOne(i => i.Combo).WithMany(c => c.Items).HasForeignKey(i => i.IdCombo);
            e.HasOne(i => i.Producto).WithMany().HasForeignKey(i => i.IdProducto);
        });

        // ---------- Ventas ----------
        modelBuilder.Entity<Venta>(e =>
        {
            e.HasKey(v => v.IdVenta);
            e.Property(v => v.Subtotal).HasPrecision(12, 2);
            e.Property(v => v.Descuento).HasPrecision(12, 2);
            e.Property(v => v.Total).HasPrecision(12, 2);
            e.Property(v => v.MontoCobrado).HasPrecision(12, 2);
            e.Property(v => v.Estado).HasConversion<string>().HasMaxLength(20);
            e.Property(v => v.Observaciones).HasMaxLength(500);
            e.Property(v => v.MotivoAnulacion).HasMaxLength(300);
            e.HasIndex(v => v.Fecha);
            e.HasIndex(v => v.Estado);
            e.HasOne(v => v.Cliente).WithMany(c => c.Ventas).HasForeignKey(v => v.IdCliente);
            e.HasOne(v => v.Usuario).WithMany().HasForeignKey(v => v.IdUsuario);
        });

        modelBuilder.Entity<DetalleVenta>(e =>
        {
            e.HasKey(d => d.IdDetalleVenta);
            e.Property(d => d.PrecioLista).HasPrecision(12, 2);
            e.Property(d => d.PorcentajeOferta).HasPrecision(5, 2);
            e.Property(d => d.PrecioUnitario).HasPrecision(12, 2);
            e.Property(d => d.CostoUnitario).HasPrecision(12, 2);
            // No es único: el mismo producto puede ir suelto y además dentro de un combo en la misma venta
            e.HasIndex(d => new { d.IdVenta, d.IdProducto });
            e.HasOne(d => d.Venta).WithMany(v => v.Detalles)
             .HasForeignKey(d => d.IdVenta).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(d => d.Producto).WithMany(p => p.DetallesVenta).HasForeignKey(d => d.IdProducto);
            e.HasOne(d => d.Oferta).WithMany().HasForeignKey(d => d.IdOferta);
            e.HasOne(d => d.Combo).WithMany().HasForeignKey(d => d.IdCombo);
        });

        modelBuilder.Entity<Cobro>(e =>
        {
            e.HasKey(c => c.IdCobro);
            e.Property(c => c.Monto).HasPrecision(12, 2);
            e.Property(c => c.Observaciones).HasMaxLength(300);
            e.HasIndex(c => c.Fecha);
            e.HasOne(c => c.Venta).WithMany(v => v.Cobros)
             .HasForeignKey(c => c.IdVenta).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(c => c.MedioPago).WithMany().HasForeignKey(c => c.IdMedioPago);
            e.HasOne(c => c.Usuario).WithMany().HasForeignKey(c => c.IdUsuario);
        });

        // ---------- Stock ----------
        modelBuilder.Entity<MovimientoStock>(e =>
        {
            e.HasKey(m => m.IdMovimientoStock);
            e.Property(m => m.Tipo).HasConversion<string>().HasMaxLength(20);
            e.Property(m => m.Motivo).HasMaxLength(300);
            e.HasIndex(m => new { m.IdProducto, m.Fecha });
            e.HasOne(m => m.Producto).WithMany(p => p.Movimientos).HasForeignKey(m => m.IdProducto);
            e.HasOne(m => m.Usuario).WithMany().HasForeignKey(m => m.IdUsuario);
            e.HasOne(m => m.Venta).WithMany().HasForeignKey(m => m.IdVenta);
            e.HasOne(m => m.Ingreso).WithMany().HasForeignKey(m => m.IdIngreso);
        });

        // ---------- Ingresos de mercadería ----------
        modelBuilder.Entity<Ingreso>(e =>
        {
            e.HasKey(i => i.IdIngreso);
            e.Property(i => i.NumeroComprobante).HasMaxLength(30);
            e.Property(i => i.Observaciones).HasMaxLength(500);
            e.Property(i => i.Total).HasPrecision(12, 2);
            e.Property(i => i.MontoPagado).HasPrecision(12, 2);
            e.Property(i => i.FechaVencimiento).HasColumnType("date");
            e.HasIndex(i => i.Fecha);
            e.HasOne(i => i.Proveedor).WithMany(p => p.Ingresos).HasForeignKey(i => i.IdProveedor);
            e.HasOne(i => i.Usuario).WithMany().HasForeignKey(i => i.IdUsuario);
        });

        modelBuilder.Entity<DetalleIngreso>(e =>
        {
            e.HasKey(d => d.IdDetalleIngreso);
            e.Property(d => d.CostoUnitario).HasPrecision(12, 2);
            e.HasIndex(d => new { d.IdIngreso, d.IdProducto }).IsUnique();
            e.HasOne(d => d.Ingreso).WithMany(i => i.Detalles)
             .HasForeignKey(d => d.IdIngreso).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(d => d.Producto).WithMany(p => p.DetallesIngreso).HasForeignKey(d => d.IdProducto);
        });

        modelBuilder.Entity<PagoProveedor>(e =>
        {
            e.HasKey(p => p.IdPagoProveedor);
            e.Property(p => p.Monto).HasPrecision(12, 2);
            e.Property(p => p.Observaciones).HasMaxLength(300);
            e.HasOne(p => p.Ingreso).WithMany(i => i.Pagos)
             .HasForeignKey(p => p.IdIngreso).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(p => p.MedioPago).WithMany().HasForeignKey(p => p.IdMedioPago);
            e.HasOne(p => p.Usuario).WithMany().HasForeignKey(p => p.IdUsuario);
        });

        // ---------- Auditoría y errores ----------
        modelBuilder.Entity<Auditoria>(e =>
        {
            e.HasKey(a => a.IdAuditoria);
            e.Property(a => a.Accion).HasMaxLength(30).IsRequired();
            e.Property(a => a.Entidad).HasMaxLength(50).IsRequired();
            e.Property(a => a.IdEntidad).HasMaxLength(50);
            e.Property(a => a.Ip).HasMaxLength(45);
            e.HasIndex(a => a.Fecha);
        });

        modelBuilder.Entity<LogError>(e =>
        {
            e.HasKey(l => l.IdLogError);
            e.Property(l => l.Mensaje).HasMaxLength(2000).IsRequired();
            e.Property(l => l.Ruta).HasMaxLength(300);
            e.Property(l => l.MetodoHttp).HasMaxLength(10);
            e.Property(l => l.Origen).HasMaxLength(10).IsRequired().HasDefaultValue(OrigenLog.Api);
            e.HasIndex(l => l.Fecha);
        });

        // Por defecto nada se borra en cascada (evita borrar ventas/ingresos sin querer
        // y el error de "multiple cascade paths" de SQL Server). Solo los detalles,
        // cobros, pagos e imágenes se borran junto con su cabecera.
        foreach (var fk in modelBuilder.Model.GetEntityTypes().SelectMany(t => t.GetForeignKeys()))
        {
            if (!EsCascadaPermitida(fk.DeclaringEntityType.ClrType, fk.PrincipalEntityType.ClrType))
                fk.DeleteBehavior = DeleteBehavior.Restrict;
        }

        SeedData(modelBuilder);
    }

    private static bool EsCascadaPermitida(Type dependiente, Type principal) =>
        (dependiente == typeof(DetalleVenta) && principal == typeof(Venta)) ||
        (dependiente == typeof(Cobro) && principal == typeof(Venta)) ||
        (dependiente == typeof(ComboItem) && principal == typeof(Combo)) ||
        (dependiente == typeof(DetalleIngreso) && principal == typeof(Ingreso)) ||
        (dependiente == typeof(PagoProveedor) && principal == typeof(Ingreso)) ||
        (dependiente == typeof(ProductoImagen) && principal == typeof(Producto));

    private static void SeedData(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Rol>().HasData(
            new Rol { IdRol = Common.Roles.IdAdmin, Nombre = Common.Roles.Admin },
            new Rol { IdRol = Common.Roles.IdOperador, Nombre = Common.Roles.Operador });

        modelBuilder.Entity<MedioPago>().HasData(
            new MedioPago { IdMedioPago = 1, Nombre = "Efectivo" },
            new MedioPago { IdMedioPago = 2, Nombre = "Tarjeta de débito" },
            new MedioPago { IdMedioPago = 3, Nombre = "Tarjeta de crédito" },
            new MedioPago { IdMedioPago = 4, Nombre = "Mercado Pago" },
            new MedioPago { IdMedioPago = 5, Nombre = "Transferencia" });

        // El administrador inicial NO se siembra acá: lo crea DbInicializador al arrancar
        // (con el email y la contraseña de la sección "AdminInicial" de appsettings).
    }

    // ================== AUDITORÍA AUTOMÁTICA ==================
    // Cada Alta / Modificación / Baja que pase por SaveChanges queda registrada
    // en la tabla Auditorias con el usuario logueado y los valores cambiados.
    // MovimientoStock no se audita: ya es en sí mismo un historial.

    private static readonly HashSet<string> CamposOcultos = new() { nameof(Usuario.PasswordHash) };

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var cambios = ChangeTracker.Entries()
            .Where(e => e.Entity is not Auditoria && e.Entity is not LogError && e.Entity is not MovimientoStock &&
                        (e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted))
            .Select(e => new CambioPendiente(e, e.State, ArmarDetalle(e)))
            .ToList();

        var resultado = await base.SaveChangesAsync(cancellationToken);

        if (cambios.Count > 0)
        {
            var http = _httpContextAccessor.HttpContext;
            var idUsuarioStr = http?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            int? idUsuario = int.TryParse(idUsuarioStr, out var id) ? id : null;
            var ip = http?.Connection.RemoteIpAddress?.ToString();

            foreach (var c in cambios)
            {
                Auditorias.Add(new Auditoria
                {
                    Fecha = Reloj.Ahora,
                    IdUsuario = idUsuario,
                    Accion = c.Estado switch
                    {
                        EntityState.Added => "Alta",
                        EntityState.Deleted => "Baja",
                        _ => "Modificacion"
                    },
                    Entidad = c.Entry.Metadata.ClrType.Name,
                    // Para las altas la clave real recién existe después de guardar
                    IdEntidad = ObtenerClave(c.Entry),
                    Detalle = c.Detalle,
                    Ip = ip
                });
            }

            await base.SaveChangesAsync(cancellationToken);
        }

        return resultado;
    }

    private static string? ObtenerClave(EntityEntry entry)
    {
        var pk = entry.Metadata.FindPrimaryKey();
        if (pk is null) return null;
        return string.Join(",", pk.Properties.Select(p => entry.Property(p.Name).CurrentValue?.ToString()));
    }

    private static string? ArmarDetalle(EntityEntry entry)
    {
        var datos = new Dictionary<string, object?>();

        foreach (var prop in entry.Properties)
        {
            var nombre = prop.Metadata.Name;
            if (CamposOcultos.Contains(nombre)) continue;

            switch (entry.State)
            {
                case EntityState.Added:
                    if (!prop.IsTemporary) datos[nombre] = prop.CurrentValue;
                    break;
                case EntityState.Deleted:
                    datos[nombre] = prop.OriginalValue;
                    break;
                case EntityState.Modified when prop.IsModified && !Equals(prop.OriginalValue, prop.CurrentValue):
                    datos[nombre] = new { antes = prop.OriginalValue, despues = prop.CurrentValue };
                    break;
            }
        }

        return datos.Count == 0 ? null : JsonSerializer.Serialize(datos);
    }

    private record CambioPendiente(EntityEntry Entry, EntityState Estado, string? Detalle);
}
