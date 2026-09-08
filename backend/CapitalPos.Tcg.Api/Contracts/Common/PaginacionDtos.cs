namespace CapitalPos.Tcg.Api.Contracts.Common;

public sealed class PagedResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public required int Total { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
}

public static class Paginacion
{
    public const int TamanoPorDefecto = 15;
    public static readonly int[] TamanosPermitidos = [15, 30, 50];

    public static bool EstaActiva(int? page, int? pageSize) => page.HasValue || pageSize.HasValue;

    public static (int Page, int PageSize) Normalizar(int? page, int? pageSize, int total)
    {
        var tamano = pageSize is int solicitado && TamanosPermitidos.Contains(solicitado)
            ? solicitado
            : TamanoPorDefecto;
        var totalPaginas = Math.Max(1, (int)Math.Ceiling(Math.Max(total, 0) / (double)tamano));
        var pagina = page is null or < 1 ? 1 : Math.Min(page.Value, totalPaginas);
        return (pagina, tamano);
    }
}
