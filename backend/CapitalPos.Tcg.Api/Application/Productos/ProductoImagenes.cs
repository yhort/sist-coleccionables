namespace CapitalPos.Tcg.Api.Application.Productos;

public static class ProductoImagenes
{
    public static IReadOnlyList<string> Parse(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return [];
        }

        return valor
            .Split([',', ';', '\n', '\r'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static string? Serializar(IEnumerable<string>? urls)
    {
        var lista = (urls ?? [])
            .SelectMany(Parse)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return lista.Length == 0 ? null : string.Join(", ", lista);
    }
}
