namespace CapitalPos.Tcg.Api.Contracts.Inventario;

public sealed class StockResumenResponse
{
    public required int Skus { get; init; }
    public required decimal Disponible { get; init; }
    public required decimal Reservado { get; init; }
    public required decimal Libre { get; init; }
}

public sealed class StockSedePagedResponse
{
    public required IReadOnlyList<StockProductoResponse> Items { get; init; }
    public required int Total { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
    public required StockResumenResponse Resumen { get; init; }
}
