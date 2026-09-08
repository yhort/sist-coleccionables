namespace CapitalPos.Tcg.Api.Application.Common;

/// <summary>
/// Códigos cortos para UI operativa. El <see cref="Guid"/> interno no se muestra.
/// Si hay correlativo/código propio se usa; si no, se recorta el identificador.
/// </summary>
public static class CodigoAmigable
{
    public const string PrefijoPedido = "PED";
    public const string PrefijoSubasta = "SUB";
    public const string PrefijoVenta = "VEN";
    public const string PrefijoItem = "ITM";

    public static string Pedido(
        Guid id,
        string? codigo = null,
        string? numeroPedido = null,
        int? correlativo = null)
    {
        if (EsCodigoPropio(codigo))
        {
            return codigo!.Trim();
        }

        if (EsCodigoPropio(numeroPedido))
        {
            return numeroPedido!.Trim();
        }

        if (correlativo is > 0)
        {
            return $"PED-{correlativo.Value:D5}";
        }

        return Formatear(id, PrefijoPedido, 6);
    }

    public static string? Pedido(Guid? id) =>
        id is Guid valor ? Pedido(valor) : null;

    public static string Subasta(Guid id) => Formatear(id, PrefijoSubasta, 4);

    public static string? Subasta(Guid? id) =>
        id is Guid valor ? Subasta(valor) : null;

    public static string Venta(Guid id) => Formatear(id, PrefijoVenta, 6);

    public static string? Venta(Guid? id) =>
        id is Guid valor ? Venta(valor) : null;

    public static string Item(Guid id) => Formatear(id, PrefijoItem, 6);

    public static bool EsGuid(string? valor) =>
        Guid.TryParse(valor, out _);

    private static bool EsCodigoPropio(string? valor)
    {
        var texto = valor?.Trim();
        return !string.IsNullOrEmpty(texto) && !EsGuid(texto);
    }

    private static string Formatear(Guid id, string prefijo, int longitud)
    {
        var hex = id.ToString("N").ToUpperInvariant();
        var recorte = Math.Clamp(longitud, 1, hex.Length);
        return $"#{prefijo}-{hex[..recorte]}";
    }
}
