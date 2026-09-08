using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Contracts.Reportes;

public sealed class KardexResumenFila
{
    public required Guid SedeId { get; init; }
    public required string SedeNombre { get; init; }
    public required Guid ProductoId { get; init; }
    public required string ProductoNombre { get; init; }
    public required string CodigoSku { get; init; }
    public required TipoProducto TipoProducto { get; init; }
    public required decimal SaldoInicial { get; init; }
    public required decimal Entradas { get; init; }
    public required decimal Salidas { get; init; }
    public required decimal SaldoFinal { get; init; }
    public required int Movimientos { get; init; }
}

public sealed class VentasPorSedeFila
{
    public required Guid SedeId { get; init; }
    public required string SedeNombre { get; init; }
    public required int CantidadVentas { get; init; }
    public required decimal Subtotal { get; init; }
    public required decimal Igv { get; init; }
    public required decimal Total { get; init; }
    public required decimal Porcentaje { get; init; }
}

public sealed class VentasPorSedeResponse
{
    public required DateTimeOffset? Desde { get; init; }
    public required DateTimeOffset? Hasta { get; init; }
    public required decimal Total { get; init; }
    public required IReadOnlyList<VentasPorSedeFila> Filas { get; init; }
}

public sealed class YieldAperturaFila
{
    public required Guid Id { get; init; }
    public required DateTimeOffset Fecha { get; init; }
    public required string SedeNombre { get; init; }
    public required string SelladoNombre { get; init; }
    public required int CantidadSellados { get; init; }
    public required int CartasObtenidas { get; init; }
    public required int CartasIngresadasKardex { get; init; }
    public required decimal CostoCompra { get; init; }
    public required decimal ValorComercial { get; init; }
    public required decimal Diferencia { get; init; }
    public decimal? YieldPorcentaje { get; init; }
}

public sealed class YieldAperturasResponse
{
    public required DateTimeOffset? Desde { get; init; }
    public required DateTimeOffset? Hasta { get; init; }
    public required decimal CostoCompra { get; init; }
    public required decimal ValorComercial { get; init; }
    public required decimal Diferencia { get; init; }
    public decimal? YieldPorcentaje { get; init; }
    public required IReadOnlyList<YieldAperturaFila> Filas { get; init; }
}
