namespace CapitalPos.Tcg.Api.Application.Comercial;

public static class IgvCalculo
{
    public const decimal Tasa = 0.18m;
    public const decimal ToleranciaPago = 0.009m;

    public static decimal Round2(decimal valor) =>
        Math.Round(valor, 2, MidpointRounding.AwayFromZero);

    public static (decimal Subtotal, decimal Igv, decimal Total) DesdeTotal(decimal total)
    {
        var totalR = Round2(total);
        var subtotal = Round2(totalR / (1 + Tasa));
        return (subtotal, Round2(totalR - subtotal), totalR);
    }

    public static (decimal ValorUnitario, decimal Subtotal, decimal Igv, decimal Total) Linea(
        decimal cantidad,
        decimal precioUnitarioConIgv)
    {
        var total = Round2(cantidad * Round2(precioUnitarioConIgv));
        var (subtotal, igv, _) = DesdeTotal(total);
        var valorUnitario = cantidad == 0 ? 0 : Round2(subtotal / cantidad);
        return (valorUnitario, subtotal, igv, total);
    }
}
