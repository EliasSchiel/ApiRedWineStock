namespace RedWineStock.Dtos;

public class MasVendidoDto
{
    public int IdProducto { get; set; }
    public string Producto { get; set; } = string.Empty;
    public string? Marca { get; set; }
    public int? Cosecha { get; set; }
    public int Cantidad { get; set; }
    public decimal Importe { get; set; }
}

public class CobradoPorMedioDto
{
    public string MedioPago { get; set; } = string.Empty;
    public decimal Monto { get; set; }
}

/// <summary>Resumen de la pantalla de inicio. Los campos marcados "solo Admin" vienen en null para el Operador.</summary>
public class DashboardDto
{
    public int VentasHoyCantidad { get; set; }
    public decimal VentasHoyTotal { get; set; }
    public decimal CobradoHoy { get; set; }
    public List<CobradoPorMedioDto> CobradoHoyPorMedio { get; set; } = new();

    public decimal VentasMesTotal { get; set; }
    public int VentasMesCantidad { get; set; }

    public decimal DeudaClientes { get; set; }
    public int ClientesConDeuda { get; set; }

    public int ProductosStockBajo { get; set; }
    public int ProductosActivos { get; set; }

    public decimal? GananciaMes { get; set; }          // solo Admin: ventas del mes - costo de lo vendido
    public decimal? DeudaProveedores { get; set; }     // solo Admin
    public decimal? ValorStockCosto { get; set; }      // solo Admin: stock x precio de compra

    public List<MasVendidoDto> MasVendidosMes { get; set; } = new();
}
