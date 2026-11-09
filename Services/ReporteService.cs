using System.Globalization;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RedWineStock.Common;
using RedWineStock.Data;
using RedWineStock.Dtos;
using RedWineStock.Models;

namespace RedWineStock.Services;

/// <summary>
/// Consultas de reportes + generación de PDF (QuestPDF). Cada PDF se devuelve en memoria (byte[]),
/// así el mismo reporte sirve para descargarlo o para adjuntarlo a un mail.
/// Las consultas (MasVendidos, Deudores, DeudasProveedores) también las usa la app en pantalla.
/// </summary>
public class ReporteService
{
    private const string ColorVino = "#7B1E3A";

    private static readonly CultureInfo Cultura = ObtenerCultura();

    private readonly AppDbContext _context;
    private readonly string _negocio;

    public ReporteService(AppDbContext context, IConfiguration config)
    {
        _context = context;
        _negocio = config["Reportes:NombreNegocio"] ?? "RedWine";
    }

    // ================== API PÚBLICA ==================

    public Task<ReporteArchivo> GenerarAsync(string tipo, ReporteFiltros filtros) => tipo switch
    {
        ReporteTipos.Stock => GenerarStockAsync(filtros.IdCategoria, soloBajo: false),
        ReporteTipos.StockBajo => GenerarStockAsync(filtros.IdCategoria, soloBajo: true),
        ReporteTipos.Ventas => GenerarVentasAsync(filtros),
        ReporteTipos.MasVendidos => GenerarMasVendidosAsync(filtros),
        ReporteTipos.Deudores => GenerarDeudoresAsync(),
        ReporteTipos.Ingresos => GenerarIngresosAsync(filtros),
        ReporteTipos.DeudasProveedores => GenerarDeudasProveedoresAsync(),
        _ => throw new ArgumentException($"Tipo de reporte desconocido: {tipo}", nameof(tipo))
    };

    /// <summary>Completa el rango de fechas. Sin fechas = mes actual. Si falta solo una, se usa el mes de la otra.</summary>
    public static (DateTime Desde, DateTime Hasta) ResolverRango(DateTime? desde, DateTime? hasta)
    {
        var refDesde = (desde ?? hasta ?? Reloj.Hoy).Date;
        var refHasta = (hasta ?? desde ?? Reloj.Hoy).Date;
        var d = desde?.Date ?? new DateTime(refDesde.Year, refDesde.Month, 1);
        var h = hasta?.Date ?? new DateTime(refHasta.Year, refHasta.Month, 1).AddMonths(1).AddDays(-1);
        return (d, h);
    }

    // ================== CONSULTAS (también para la pantalla de la app) ==================

    public async Task<List<MasVendidoDto>> MasVendidosAsync(DateTime desde, DateTime hasta, int top)
    {
        var d = desde.Date;
        var h = hasta.Date.AddDays(1);

        return await _context.DetallesVenta.AsNoTracking()
            .Where(x => x.Venta!.Estado != EstadoVenta.Anulada && x.Venta.Fecha >= d && x.Venta.Fecha < h)
            .GroupBy(x => new { x.IdProducto, x.Producto!.Nombre, Marca = x.Producto.Marca!.Nombre, x.Producto.Cosecha })
            .Select(g => new MasVendidoDto
            {
                IdProducto = g.Key.IdProducto,
                Producto = g.Key.Nombre,
                Marca = g.Key.Marca,
                Cosecha = g.Key.Cosecha,
                Cantidad = g.Sum(x => x.Cantidad),
                Importe = g.Sum(x => x.Cantidad * x.PrecioUnitario)
            })
            .OrderByDescending(x => x.Cantidad).ThenByDescending(x => x.Importe)
            .Take(top)
            .ToListAsync();
    }

    public async Task<List<DeudorDto>> DeudoresAsync()
    {
        var grupos = await _context.Ventas.AsNoTracking()
            .Where(v => v.Estado == EstadoVenta.Pendiente && v.IdCliente != null)
            .GroupBy(v => v.IdCliente)
            .Select(g => new
            {
                IdCliente = g.Key,
                Cantidad = g.Count(),
                Deuda = g.Sum(v => v.Total - v.MontoCobrado),
                Antigua = g.Min(v => v.Fecha)
            })
            .ToListAsync();

        var ids = grupos.Select(g => g.IdCliente!.Value).ToList();
        var clientes = await _context.Clientes.AsNoTracking()
            .Where(c => ids.Contains(c.IdCliente))
            .ToDictionaryAsync(c => c.IdCliente);

        return grupos
            .Where(g => g.Deuda > 0 && clientes.ContainsKey(g.IdCliente!.Value))
            .Select(g =>
            {
                var c = clientes[g.IdCliente!.Value];
                return new DeudorDto
                {
                    IdCliente = c.IdCliente,
                    Nombre = c.Nombre,
                    Telefono = c.Telefono,
                    CantidadVentas = g.Cantidad,
                    Deuda = g.Deuda,
                    VentaMasAntigua = g.Antigua
                };
            })
            .OrderByDescending(d => d.Deuda)
            .ToList();
    }

    public async Task<List<DeudaProveedorDto>> DeudasProveedoresAsync()
    {
        var hoy = Reloj.Hoy;
        var grupos = await _context.Ingresos.AsNoTracking()
            .Where(i => i.Total > i.MontoPagado)
            .GroupBy(i => i.IdProveedor)
            .Select(g => new
            {
                IdProveedor = g.Key,
                Cantidad = g.Count(),
                Deuda = g.Sum(i => i.Total - i.MontoPagado),
                Proximo = g.Min(i => i.FechaVencimiento),
                Vencidas = g.Count(i => i.FechaVencimiento != null && i.FechaVencimiento < hoy)
            })
            .ToListAsync();

        var ids = grupos.Select(g => g.IdProveedor).ToList();
        var proveedores = await _context.Proveedores.AsNoTracking()
            .Where(p => ids.Contains(p.IdProveedor))
            .ToDictionaryAsync(p => p.IdProveedor);

        return grupos
            .Where(g => proveedores.ContainsKey(g.IdProveedor))
            .Select(g =>
            {
                var p = proveedores[g.IdProveedor];
                return new DeudaProveedorDto
                {
                    IdProveedor = p.IdProveedor,
                    Nombre = p.Nombre,
                    Telefono = p.Telefono,
                    CantidadIngresos = g.Cantidad,
                    Deuda = g.Deuda,
                    ProximoVencimiento = g.Proximo,
                    EntregasVencidas = g.Vencidas
                };
            })
            .OrderByDescending(d => d.Deuda)
            .ToList();
    }

    // ================== STOCK / STOCK BAJO ==================

    private record FilaStock(string Producto, string Categoria, int Stock, int StockMinimo, decimal Precio)
    {
        public bool Bajo => Stock <= StockMinimo;
        public decimal Valor => Stock * Precio;
    }

    private async Task<ReporteArchivo> GenerarStockAsync(int? idCategoria, bool soloBajo)
    {
        var query = _context.Productos.AsNoTracking().Where(p => p.Activo);
        var filtros = new List<string>();

        if (idCategoria.HasValue)
        {
            var id = idCategoria.Value;
            query = query.Where(p => p.IdCategoria == id);
            var nombre = await _context.Categorias.AsNoTracking()
                .Where(c => c.IdCategoria == id).Select(c => c.Nombre).FirstOrDefaultAsync();
            filtros.Add($"Categoría: {nombre ?? id.ToString()}");
        }
        else
        {
            filtros.Add("Categoría: todas");
        }

        if (soloBajo)
        {
            query = query.Where(p => p.Stock <= p.StockMinimo);
            filtros.Add("Productos activos con stock menor o igual al stock mínimo");
        }
        else
        {
            filtros.Add("Solo productos activos. Valor calculado a precio de venta.");
        }

        var datos = await query
            .OrderBy(p => p.Categoria!.Nombre).ThenBy(p => p.Nombre)
            .Select(p => new { p.Nombre, Categoria = p.Categoria!.Nombre, p.Stock, p.StockMinimo, p.PrecioVenta })
            .ToListAsync();

        var filas = datos
            .Select(d => new FilaStock(d.Nombre, d.Categoria, d.Stock, d.StockMinimo, d.PrecioVenta))
            .ToList();

        var titulo = soloBajo ? "Reporte de productos con stock bajo" : "Reporte de stock";

        var pdf = Armar(titulo, filtros, contenedor =>
        {
            contenedor.Column(col =>
            {
                if (filas.Count == 0)
                {
                    col.Item().Text("No hay productos para los filtros indicados.");
                    return;
                }

                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(4);
                        c.RelativeColumn(2.5f);
                        c.RelativeColumn(1);
                        c.RelativeColumn(1.2f);
                        c.RelativeColumn(2);
                        c.RelativeColumn(2.2f);
                        c.RelativeColumn(1.6f);
                    });

                    t.Header(h =>
                    {
                        Cab(h, "Producto");
                        Cab(h, "Categoría");
                        Cab(h, "Stock", derecha: true);
                        Cab(h, "Mínimo", derecha: true);
                        Cab(h, "Precio", derecha: true);
                        Cab(h, "Valor", derecha: true);
                        Cab(h, "Estado");
                    });

                    foreach (var f in filas)
                    {
                        Dato(t, f.Producto, alerta: f.Bajo);
                        Dato(t, f.Categoria, alerta: f.Bajo);
                        Dato(t, f.Stock.ToString(Cultura), derecha: true, alerta: f.Bajo);
                        Dato(t, f.StockMinimo.ToString(Cultura), derecha: true, alerta: f.Bajo);
                        Dato(t, Dinero(f.Precio), derecha: true, alerta: f.Bajo);
                        Dato(t, Dinero(f.Valor), derecha: true, alerta: f.Bajo);
                        Dato(t, f.Stock == 0 ? "Sin stock" : f.Bajo ? "Stock bajo" : "OK", alerta: f.Bajo);
                    }
                });

                col.Item().PaddingTop(10).AlignRight().Width(260).Column(tot =>
                {
                    Total(tot, "Productos listados", filas.Count.ToString(Cultura));
                    Total(tot, "Unidades en stock", filas.Sum(f => f.Stock).ToString(Cultura));
                    Total(tot, "Productos con stock bajo", filas.Count(f => f.Bajo).ToString(Cultura));
                    if (!soloBajo)
                        Total(tot, "Valor del stock (precio de venta)", Dinero(filas.Sum(f => f.Valor)), destacado: true);
                });
            });
        });

        return new ReporteArchivo(pdf, NombreArchivo(soloBajo ? ReporteTipos.StockBajo : ReporteTipos.Stock), titulo);
    }

    // ================== VENTAS ==================

    private async Task<ReporteArchivo> GenerarVentasAsync(ReporteFiltros f)
    {
        var (desde, hasta) = ResolverRango(f.Desde, f.Hasta);
        var h1 = hasta.AddDays(1);

        var query = _context.Ventas.AsNoTracking().Where(v => v.Fecha >= desde && v.Fecha < h1);
        if (f.Estado.HasValue) query = query.Where(v => v.Estado == f.Estado);

        var ventas = await query
            .Include(v => v.Cliente)
            .OrderBy(v => v.Fecha)
            .ToListAsync();

        var filtros = new List<string>
        {
            $"Período: {Dia(desde)} al {Dia(hasta)}",
            $"Estado: {(f.Estado.HasValue ? f.Estado.ToString() : "todos")}"
        };

        const string titulo = "Reporte de ventas";

        var pdf = Armar(titulo, filtros, contenedor =>
        {
            contenedor.Column(col =>
            {
                if (ventas.Count == 0)
                {
                    col.Item().Text("No hay ventas para los filtros indicados.");
                    return;
                }

                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(1.1f);
                        c.RelativeColumn(2.6f);
                        c.RelativeColumn(3.6f);
                        c.RelativeColumn(1.8f);
                        c.RelativeColumn(2.2f);
                        c.RelativeColumn(2.2f);
                        c.RelativeColumn(2.2f);
                    });

                    t.Header(h =>
                    {
                        Cab(h, "N°");
                        Cab(h, "Fecha");
                        Cab(h, "Cliente");
                        Cab(h, "Estado");
                        Cab(h, "Total", derecha: true);
                        Cab(h, "Cobrado", derecha: true);
                        Cab(h, "Saldo", derecha: true);
                    });

                    foreach (var v in ventas)
                    {
                        var anulada = v.Estado == EstadoVenta.Anulada;
                        Dato(t, v.IdVenta.ToString(Cultura), alerta: anulada);
                        Dato(t, Fecha(v.Fecha), alerta: anulada);
                        Dato(t, v.Cliente?.Nombre ?? "Mostrador", alerta: anulada);
                        Dato(t, v.Estado.ToString(), alerta: anulada);
                        Dato(t, Dinero(v.Total), derecha: true, alerta: anulada);
                        Dato(t, Dinero(v.MontoCobrado), derecha: true, alerta: anulada);
                        Dato(t, Dinero(v.Saldo), derecha: true, alerta: anulada || v.Saldo > 0);
                    }
                });

                var validas = ventas.Where(v => v.Estado != EstadoVenta.Anulada).ToList();
                col.Item().PaddingTop(10).AlignRight().Width(280).Column(tot =>
                {
                    Total(tot, "Ventas válidas", validas.Count.ToString(Cultura));
                    Total(tot, "Ventas anuladas", ventas.Count(v => v.Estado == EstadoVenta.Anulada).ToString(Cultura));
                    Total(tot, "Descuentos otorgados", Dinero(validas.Sum(v => v.Descuento)));
                    Total(tot, "Total vendido", Dinero(validas.Sum(v => v.Total)), destacado: true);
                    Total(tot, "Cobrado", Dinero(validas.Sum(v => v.MontoCobrado)));
                    Total(tot, "Pendiente de cobro", Dinero(validas.Sum(v => v.Saldo)));
                });
            });
        });

        return new ReporteArchivo(pdf, NombreArchivo(ReporteTipos.Ventas), titulo);
    }

    // ================== MÁS VENDIDOS ==================

    private async Task<ReporteArchivo> GenerarMasVendidosAsync(ReporteFiltros f)
    {
        var (desde, hasta) = ResolverRango(f.Desde, f.Hasta);
        var filas = await MasVendidosAsync(desde, hasta, 50);

        var filtros = new List<string>
        {
            $"Período: {Dia(desde)} al {Dia(hasta)}",
            "Ventas anuladas excluidas. Importe sin el descuento final de cada venta."
        };
        const string titulo = "Productos más vendidos";

        var pdf = Armar(titulo, filtros, contenedor =>
        {
            contenedor.Column(col =>
            {
                if (filas.Count == 0)
                {
                    col.Item().Text("No hay ventas en el período indicado.");
                    return;
                }

                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(0.8f);
                        c.RelativeColumn(6);
                        c.RelativeColumn(1.8f);
                        c.RelativeColumn(2.6f);
                    });

                    t.Header(h =>
                    {
                        Cab(h, "#");
                        Cab(h, "Producto");
                        Cab(h, "Unidades", derecha: true);
                        Cab(h, "Importe", derecha: true);
                    });

                    var puesto = 1;
                    foreach (var m in filas)
                    {
                        Dato(t, (puesto++).ToString(Cultura));
                        Dato(t, m.Producto);
                        Dato(t, m.Cantidad.ToString(Cultura), derecha: true);
                        Dato(t, Dinero(m.Importe), derecha: true);
                    }
                });

                col.Item().PaddingTop(10).AlignRight().Width(260).Column(tot =>
                {
                    Total(tot, "Unidades vendidas", filas.Sum(m => m.Cantidad).ToString(Cultura));
                    Total(tot, "Importe total", Dinero(filas.Sum(m => m.Importe)), destacado: true);
                });
            });
        });

        return new ReporteArchivo(pdf, NombreArchivo(ReporteTipos.MasVendidos), titulo);
    }

    // ================== DEUDORES ==================

    private async Task<ReporteArchivo> GenerarDeudoresAsync()
    {
        var filas = await DeudoresAsync();
        const string titulo = "Clientes con saldo pendiente";

        var pdf = Armar(titulo, new[] { $"Situación al {Fecha(Reloj.Ahora)}" }, contenedor =>
        {
            contenedor.Column(col =>
            {
                if (filas.Count == 0)
                {
                    col.Item().Text("No hay clientes con saldo pendiente.");
                    return;
                }

                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(4);
                        c.RelativeColumn(2.6f);
                        c.RelativeColumn(1.4f);
                        c.RelativeColumn(2.4f);
                        c.RelativeColumn(2.6f);
                    });

                    t.Header(h =>
                    {
                        Cab(h, "Cliente");
                        Cab(h, "Teléfono");
                        Cab(h, "Ventas", derecha: true);
                        Cab(h, "Desde");
                        Cab(h, "Deuda", derecha: true);
                    });

                    foreach (var d in filas)
                    {
                        Dato(t, d.Nombre);
                        Dato(t, d.Telefono ?? "-");
                        Dato(t, d.CantidadVentas.ToString(Cultura), derecha: true);
                        Dato(t, Dia(d.VentaMasAntigua));
                        Dato(t, Dinero(d.Deuda), derecha: true, alerta: true);
                    }
                });

                col.Item().PaddingTop(10).AlignRight().Width(260).Column(tot =>
                {
                    Total(tot, "Clientes con deuda", filas.Count.ToString(Cultura));
                    Total(tot, "Total a cobrar", Dinero(filas.Sum(d => d.Deuda)), destacado: true);
                });
            });
        });

        return new ReporteArchivo(pdf, NombreArchivo(ReporteTipos.Deudores), titulo);
    }

    // ================== INGRESOS (Admin) ==================

    private async Task<ReporteArchivo> GenerarIngresosAsync(ReporteFiltros f)
    {
        var (desde, hasta) = ResolverRango(f.Desde, f.Hasta);
        var h1 = hasta.AddDays(1);

        var query = _context.Ingresos.AsNoTracking().Where(i => i.Fecha >= desde && i.Fecha < h1);
        var filtros = new List<string> { $"Período: {Dia(desde)} al {Dia(hasta)}" };

        if (f.IdProveedor.HasValue)
        {
            var id = f.IdProveedor.Value;
            query = query.Where(i => i.IdProveedor == id);
            var nombre = await _context.Proveedores.AsNoTracking()
                .Where(p => p.IdProveedor == id).Select(p => p.Nombre).FirstOrDefaultAsync();
            filtros.Add($"Proveedor: {nombre ?? id.ToString()}");
        }
        else
        {
            filtros.Add("Proveedor: todos");
        }

        var ingresos = await query.Include(i => i.Proveedor).OrderBy(i => i.Fecha).ToListAsync();
        const string titulo = "Reporte de ingresos de mercadería";

        var pdf = Armar(titulo, filtros, contenedor =>
        {
            contenedor.Column(col =>
            {
                if (ingresos.Count == 0)
                {
                    col.Item().Text("No hay ingresos para los filtros indicados.");
                    return;
                }

                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(1.1f);
                        c.RelativeColumn(2.6f);
                        c.RelativeColumn(3.4f);
                        c.RelativeColumn(2.2f);
                        c.RelativeColumn(2.2f);
                        c.RelativeColumn(2.2f);
                        c.RelativeColumn(2.2f);
                    });

                    t.Header(h =>
                    {
                        Cab(h, "N°");
                        Cab(h, "Fecha");
                        Cab(h, "Proveedor");
                        Cab(h, "Comprobante");
                        Cab(h, "Total", derecha: true);
                        Cab(h, "Pagado", derecha: true);
                        Cab(h, "Saldo", derecha: true);
                    });

                    foreach (var i in ingresos)
                    {
                        Dato(t, i.IdIngreso.ToString(Cultura));
                        Dato(t, Fecha(i.Fecha));
                        Dato(t, i.Proveedor?.Nombre ?? "-");
                        Dato(t, i.NumeroComprobante ?? "-");
                        Dato(t, Dinero(i.Total), derecha: true);
                        Dato(t, Dinero(i.MontoPagado), derecha: true);
                        Dato(t, Dinero(i.Saldo), derecha: true, alerta: i.Saldo > 0);
                    }
                });

                col.Item().PaddingTop(10).AlignRight().Width(260).Column(tot =>
                {
                    Total(tot, "Cantidad de ingresos", ingresos.Count.ToString(Cultura));
                    Total(tot, "Total comprado", Dinero(ingresos.Sum(i => i.Total)), destacado: true);
                    Total(tot, "Pagado", Dinero(ingresos.Sum(i => i.MontoPagado)));
                    Total(tot, "Adeudado", Dinero(ingresos.Sum(i => i.Saldo)));
                });
            });
        });

        return new ReporteArchivo(pdf, NombreArchivo(ReporteTipos.Ingresos), titulo);
    }

    // ================== DEUDAS CON PROVEEDORES (Admin) ==================

    private async Task<ReporteArchivo> GenerarDeudasProveedoresAsync()
    {
        var filas = await DeudasProveedoresAsync();
        const string titulo = "Deudas con proveedores";

        var pdf = Armar(titulo, new[] { $"Situación al {Fecha(Reloj.Ahora)}" }, contenedor =>
        {
            contenedor.Column(col =>
            {
                if (filas.Count == 0)
                {
                    col.Item().Text("No hay deudas con proveedores.");
                    return;
                }

                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(4.2f);
                        c.RelativeColumn(2.6f);
                        c.RelativeColumn(1.6f);
                        c.RelativeColumn(2.8f);
                        c.RelativeColumn(2.8f);
                    });

                    t.Header(h =>
                    {
                        Cab(h, "Proveedor");
                        Cab(h, "Teléfono");
                        Cab(h, "Entregas", derecha: true);
                        Cab(h, "Vence", derecha: true);
                        Cab(h, "Deuda", derecha: true);
                    });

                    foreach (var d in filas)
                    {
                        Dato(t, d.Nombre);
                        Dato(t, d.Telefono ?? "-");
                        Dato(t, d.CantidadIngresos.ToString(Cultura), derecha: true);
                        Dato(t, d.ProximoVencimiento.HasValue ? d.ProximoVencimiento.Value.ToString("dd/MM/yyyy", Cultura) : "-",
                            derecha: true, alerta: d.EntregasVencidas > 0);
                        Dato(t, Dinero(d.Deuda), derecha: true, alerta: true);
                    }
                });

                col.Item().PaddingTop(10).AlignRight().Width(260).Column(tot =>
                {
                    Total(tot, "Proveedores con deuda", filas.Count.ToString(Cultura));
                    Total(tot, "Total adeudado", Dinero(filas.Sum(d => d.Deuda)), destacado: true);
                });
            });
        });

        return new ReporteArchivo(pdf, NombreArchivo(ReporteTipos.DeudasProveedores), titulo);
    }

    // ================== COMPROBANTE DE VENTA ==================

    /// <summary>Comprobante simple de una venta (NO es una factura). Devuelve null si la venta no existe.</summary>
    public async Task<ReporteArchivo?> GenerarComprobanteAsync(int idVenta)
    {
        var venta = await _context.Ventas.AsNoTracking()
            .Include(v => v.Cliente)
            .Include(v => v.Usuario)
            .Include(v => v.Detalles).ThenInclude(d => d.Producto)
            .Include(v => v.Cobros).ThenInclude(c => c.MedioPago)
            .AsSplitQuery()
            .FirstOrDefaultAsync(v => v.IdVenta == idVenta);
        if (venta is null) return null;

        var titulo = $"Comprobante de venta N° {venta.IdVenta}";
        var lineas = new List<string>
        {
            $"Fecha: {Fecha(venta.Fecha)}",
            $"Cliente: {venta.Cliente?.Nombre ?? "Consumidor final"}",
            $"Atendió: {venta.Usuario?.Nombre ?? "-"}"
        };
        if (venta.Estado == EstadoVenta.Anulada) lineas.Add("VENTA ANULADA");

        var pdf = Armar(titulo, lineas, contenedor =>
        {
            contenedor.Column(col =>
            {
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(1.2f);
                        c.RelativeColumn(6);
                        c.RelativeColumn(2.4f);
                        c.RelativeColumn(2.6f);
                    });

                    t.Header(h =>
                    {
                        Cab(h, "Cant.");
                        Cab(h, "Producto");
                        Cab(h, "Precio", derecha: true);
                        Cab(h, "Subtotal", derecha: true);
                    });

                    foreach (var d in venta.Detalles)
                    {
                        var nombre = d.PorcentajeOferta > 0
                            ? $"{d.Producto?.Nombre} (oferta -{d.PorcentajeOferta.ToString("0.##", Cultura)}%)"
                            : d.Producto?.Nombre ?? "-";
                        Dato(t, d.Cantidad.ToString(Cultura));
                        Dato(t, nombre);
                        Dato(t, Dinero(d.PrecioUnitario), derecha: true);
                        Dato(t, Dinero(d.Subtotal), derecha: true);
                    }
                });

                col.Item().PaddingTop(10).AlignRight().Width(260).Column(tot =>
                {
                    Total(tot, "Subtotal", Dinero(venta.Subtotal));
                    if (venta.Descuento > 0) Total(tot, "Descuento", "- " + Dinero(venta.Descuento));
                    Total(tot, "TOTAL", Dinero(venta.Total), destacado: true);
                });

                if (venta.Cobros.Count > 0)
                {
                    col.Item().PaddingTop(12).Text("Pagos recibidos").SemiBold();
                    foreach (var c in venta.Cobros.OrderBy(c => c.Fecha))
                        col.Item().Text($"{Fecha(c.Fecha)}  -  {c.MedioPago?.Nombre}: {Dinero(c.Monto)}");
                }

                if (venta.Estado == EstadoVenta.Pendiente)
                    col.Item().PaddingTop(8).Text($"Saldo pendiente de pago: {Dinero(venta.Saldo)}").Bold().FontColor(Colors.Red.Darken2);

                col.Item().PaddingTop(20).Text("Comprobante interno. No es válido como factura.")
                    .FontSize(8).FontColor(Colors.Grey.Darken1);
            });
        });

        return new ReporteArchivo(pdf, $"comprobante-venta-{venta.IdVenta}.pdf", titulo);
    }

    // ================== ARMADO COMÚN DEL PDF ==================

    private byte[] Armar(string titulo, IEnumerable<string> filtros, Action<IContainer> contenido)
    {
        var emision = Reloj.Ahora;
        var lineasFiltros = filtros.ToList();

        return Document.Create(documento =>
        {
            documento.Page(pagina =>
            {
                pagina.Size(PageSizes.A4);
                pagina.Margin(30);
                pagina.DefaultTextStyle(x => x.FontSize(9));

                pagina.Header().Element(c => Encabezado(c, titulo, emision, lineasFiltros));
                pagina.Content().PaddingVertical(10).Element(contenido);

                pagina.Footer().Row(fila =>
                {
                    fila.RelativeItem().Text(_negocio).FontSize(8).FontColor(Colors.Grey.Darken1);
                    fila.RelativeItem().AlignRight().Text(texto =>
                    {
                        texto.DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Darken1));
                        texto.Span("Página ");
                        texto.CurrentPageNumber();
                        texto.Span(" de ");
                        texto.TotalPages();
                    });
                });
            });
        }).GeneratePdf();
    }

    private void Encabezado(IContainer contenedor, string titulo, DateTime emision, List<string> filtros)
    {
        contenedor.PaddingBottom(8).BorderBottom(1).BorderColor(Colors.Grey.Medium).Column(col =>
        {
            col.Item().Row(fila =>
            {
                fila.RelativeItem().Text(_negocio).FontSize(16).Bold().FontColor(ColorVino);
                fila.ConstantItem(160).AlignRight().Text($"Emitido: {Fecha(emision)}").FontSize(8);
            });
            col.Item().PaddingTop(2).Text(titulo).FontSize(12).SemiBold();
            foreach (var filtro in filtros)
                col.Item().Text(filtro).FontSize(8).FontColor(Colors.Grey.Darken2);
        });
    }

    // ---- estilos de tabla ----

    private static IContainer CeldaCab(IContainer c) =>
        c.Background(ColorVino).Padding(4)
         .DefaultTextStyle(x => x.FontColor(Colors.White).SemiBold());

    private static IContainer Celda(IContainer c) =>
        c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3).PaddingHorizontal(4);

    private static void Cab(TableCellDescriptor h, string texto, bool derecha = false)
    {
        IContainer celda = h.Cell().Element(CeldaCab);
        if (derecha) celda = celda.AlignRight();
        celda.Text(texto);
    }

    private static void Dato(TableDescriptor t, string texto, bool derecha = false, bool alerta = false,
        bool negrita = false, uint span = 1)
    {
        var contenedor = t.Cell();
        if (span > 1) contenedor = contenedor.ColumnSpan(span);

        IContainer celda = contenedor.Element(Celda);
        if (derecha) celda = celda.AlignRight();

        var txt = celda.Text(texto);
        if (negrita) txt.Bold();
        if (alerta) txt.FontColor(Colors.Red.Darken2);
    }

    private static void Total(ColumnDescriptor col, string etiqueta, string valor, bool destacado = false)
    {
        col.Item().PaddingVertical(1).Row(fila =>
        {
            var izq = fila.RelativeItem().Text(etiqueta);
            var der = fila.AutoItem().Text(valor);
            if (destacado)
            {
                izq.Bold();
                der.Bold();
            }
        });
    }

    // ---- formatos ----

    private static string Dinero(decimal monto) => "$ " + monto.ToString("N2", Cultura);
    private static string Fecha(DateTime fecha) => fecha.ToString("dd/MM/yyyy HH:mm", Cultura);
    private static string Dia(DateTime fecha) => fecha.ToString("dd/MM/yyyy", Cultura);
    private static string NombreArchivo(string tipo) => $"reporte-{tipo}-{Reloj.Ahora:yyyyMMdd-HHmm}.pdf";

    private static CultureInfo ObtenerCultura()
    {
        try { return CultureInfo.GetCultureInfo("es-AR"); }
        catch (CultureNotFoundException) { return CultureInfo.InvariantCulture; }
    }
}
