using RedWineStock.Common;
using RedWineStock.Data;
using RedWineStock.Models;

namespace RedWineStock.Services;

/// <summary>
/// ÚNICO lugar donde cambia el stock de un producto. Cada cambio modifica Producto.Stock
/// y agrega la fila correspondiente en MovimientosStock (historial). Lo usan Ventas, Ingresos y Ajustes.
/// No guarda: el que lo llama hace SaveChanges dentro de su transacción.
/// </summary>
public class StockService
{
    private readonly AppDbContext _context;
    public StockService(AppDbContext context) => _context = context;

    /// <param name="cantidad">Con signo: positiva suma stock, negativa resta.</param>
    public MovimientoStock Mover(Producto producto, TipoMovimiento tipo, int cantidad, int idUsuario,
        string? motivo = null, Venta? venta = null, Ingreso? ingreso = null)
    {
        producto.Stock += cantidad;

        var movimiento = new MovimientoStock
        {
            Fecha = Reloj.Ahora,
            Producto = producto,
            Tipo = tipo,
            Cantidad = cantidad,
            StockResultante = producto.Stock,
            Motivo = motivo,
            IdUsuario = idUsuario,
            Venta = venta,
            Ingreso = ingreso
        };
        _context.MovimientosStock.Add(movimiento);
        return movimiento;
    }
}
