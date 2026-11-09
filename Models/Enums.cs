namespace RedWineStock.Models;

public enum EstadoVenta
{
    Cobrada,    // el total ya está cobrado
    Pendiente,  // "a pagar más tarde" o con saldo; exige cliente
    Anulada     // se anuló: el stock volvió al inventario
}

public enum TipoMovimiento
{
    Entrada,    // ingreso de mercadería
    Venta,      // salida por venta
    Ajuste,     // corrección manual con motivo (+ o -)
    Anulacion   // vuelve el stock de una venta anulada
}
