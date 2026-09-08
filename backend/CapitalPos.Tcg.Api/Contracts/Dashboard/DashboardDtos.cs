namespace CapitalPos.Tcg.Api.Contracts.Dashboard;

public sealed class DashboardResumenResponse
{
    public required DateOnly FechaLocal { get; init; }
    public required DateTimeOffset GeneradoEn { get; init; }
    public required VentasDiaKpi VentasHoy { get; init; }
    public required SubastasKpi Subastas { get; init; }
    public required PagosIpnKpi PagosNotificados { get; init; }
    public required StockBajoKpi StockBajo { get; init; }
    public required IReadOnlyList<ActividadRecienteItem> ActividadReciente { get; init; }
}

public sealed class VentasDiaKpi
{
    public required decimal TotalRecaudado { get; init; }
    public required int ComprobantesEmitidos { get; init; }
    public required int VentasRegistradas { get; init; }
}

public sealed class SubastasKpi
{
    public required int Activas { get; init; }
    public required int GanadoresPendientePago { get; init; }
}

public sealed class PagosIpnKpi
{
    public required int PendientesAsociar { get; init; }
    public required decimal MontoPendiente { get; init; }
}

public sealed class StockBajoKpi
{
    public required int Cantidad { get; init; }
    public required decimal Umbral { get; init; }
}

public sealed class ActividadRecienteItem
{
    public required Guid Id { get; init; }
    public required string Tipo { get; init; }
    public required string ClienteNombre { get; init; }
    public required decimal Monto { get; init; }
    public required string Estado { get; init; }
    public required string MetodoPago { get; init; }
    public required DateTimeOffset Fecha { get; init; }
}
