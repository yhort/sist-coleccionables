using CapitalPos.Tcg.Api.Contracts.Aperturas;
using CapitalPos.Tcg.Api.Domain.Entities;

namespace CapitalPos.Tcg.Api.Application.Aperturas;

public static class YieldApertura
{
    public static decimal CostoSellado(Producto? sellado, int cantidadSellados)
    {
        if (sellado is null || cantidadSellados <= 0)
        {
            return 0;
        }

        var unitario = sellado.Costo ?? sellado.PrecioVenta;
        return unitario * cantidadSellados;
    }

    public static decimal PrecioVigente(Producto? carta) => carta?.PrecioVenta ?? 0;

    public static decimal ValorEstimadoCartas(
        IEnumerable<(Producto? Carta, int Cantidad)> lineas)
    {
        return lineas.Sum(linea => PrecioVigente(linea.Carta) * linea.Cantidad);
    }

    public static RendimientoAperturaDto Calcular(decimal costoSellado, decimal valorCartas)
    {
        var diferencia = valorCartas - costoSellado;
        return new RendimientoAperturaDto
        {
            CostoSellado = Round2(costoSellado),
            ValorEstimadoCartas = Round2(valorCartas),
            Diferencia = Round2(diferencia),
            YieldPorcentaje = costoSellado > 0
                ? Round2(diferencia / costoSellado * 100)
                : null
        };
    }

    public static decimal[] Prorratear(
        IReadOnlyList<(Producto? Carta, int Cantidad)> lineas,
        decimal costoSellado)
    {
        var valores = lineas
            .Select(linea => PrecioVigente(linea.Carta) * linea.Cantidad)
            .ToArray();
        var totalValor = valores.Sum();
        var totalUnidades = lineas.Sum(l => l.Cantidad);

        return lineas.Select((linea, index) =>
        {
            if (linea.Cantidad <= 0)
            {
                return 0m;
            }

            if (totalValor > 0)
            {
                return Round4(valores[index] / totalValor * costoSellado / linea.Cantidad);
            }

            return totalUnidades > 0 ? Round4(costoSellado / totalUnidades) : 0m;
        }).ToArray();
    }

    public static decimal Round2(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);

    public static decimal Round4(decimal valor) => Math.Round(valor, 4, MidpointRounding.AwayFromZero);
}
