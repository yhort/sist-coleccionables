using CapitalPos.Tcg.Api.Application.Comercial;
using CapitalPos.Tcg.Api.Contracts.Cpe;
using CapitalPos.Tcg.Api.Domain.Entities;

namespace CapitalPos.Tcg.Api.Application.Cpe;

/// <summary>
/// Resuelve las líneas de una NC (total vs parcial por ítem) a partir de la venta.
/// </summary>
public static class NotaCreditoLineas
{
    public readonly record struct LineaResuelta(
        Guid ProductoId,
        string CodigoSku,
        string Descripcion,
        decimal Cantidad,
        decimal PrecioUnitario,
        decimal ValorUnitario,
        decimal Subtotal,
        decimal Igv,
        decimal Total,
        string CodigoAfectacionIgv);

    public static IReadOnlyList<LineaResuelta> Resolver(Venta venta, EmitirNotaCreditoRequest? nota)
    {
        var codigo = Catalogo09Motivos.NormalizarCodigo(nota?.CodigoMotivo);
        if (Catalogo09Motivos.EsDevolucionPorItem(codigo))
        {
            return ResolverParcial(venta, nota?.Items);
        }

        // 01 / 06 (y resto): documento completo; se ignoran ítems del request.
        return venta.Detalles.Select(DesdeDetalle).ToList();
    }

    public static bool EsAnulacionTotal(Venta venta, EmitirNotaCreditoRequest? nota)
    {
        var codigo = Catalogo09Motivos.NormalizarCodigo(nota?.CodigoMotivo);
        if (Catalogo09Motivos.EsDocumentoCompleto(codigo))
        {
            return true;
        }

        if (!Catalogo09Motivos.EsDevolucionPorItem(codigo))
        {
            return true;
        }

        var lineas = Resolver(venta, nota);
        if (lineas.Count != venta.Detalles.Count)
        {
            return false;
        }

        foreach (var detalle in venta.Detalles)
        {
            var linea = lineas.FirstOrDefault(l => l.ProductoId == detalle.ProductoId);
            if (linea.ProductoId == Guid.Empty || linea.Cantidad != detalle.Cantidad)
            {
                return false;
            }
        }

        return true;
    }

    private static IReadOnlyList<LineaResuelta> ResolverParcial(
        Venta venta,
        IReadOnlyList<EmitirNotaCreditoItemRequest>? items)
    {
        if (items is null || items.Count == 0)
        {
            throw new BusinessRuleException(
                "La devolución por ítem (motivo 07) exige seleccionar al menos un producto con cantidad.");
        }

        var resultado = new List<LineaResuelta>();
        var usados = new HashSet<Guid>();

        foreach (var item in items)
        {
            if (item.Cantidad <= 0)
            {
                continue;
            }

            VentaDetalle? origen = null;
            if (item.VentaDetalleId is { } detalleId && detalleId != Guid.Empty)
            {
                origen = venta.Detalles.FirstOrDefault(d => d.Id == detalleId);
            }

            origen ??= venta.Detalles.FirstOrDefault(d => d.ProductoId == item.ProductoId);
            if (origen is null)
            {
                throw new BusinessRuleException(
                    "Hay productos en la nota de crédito que no pertenecen a la venta original.");
            }

            if (!usados.Add(origen.Id))
            {
                throw new BusinessRuleException(
                    $"El producto «{origen.Descripcion}» está duplicado en la nota de crédito.");
            }

            if (item.Cantidad > origen.Cantidad)
            {
                throw new BusinessRuleException(
                    $"La cantidad a devolver de «{origen.Descripcion}» supera la del comprobante original.");
            }

            var (valorUnitario, subtotal, igv, total) = IgvCalculo.Linea(item.Cantidad, origen.PrecioUnitario);
            resultado.Add(new LineaResuelta(
                origen.ProductoId,
                origen.CodigoSku,
                origen.Descripcion,
                IgvCalculo.Round2(item.Cantidad),
                IgvCalculo.Round2(origen.PrecioUnitario),
                valorUnitario,
                subtotal,
                igv,
                total,
                string.IsNullOrWhiteSpace(origen.CodigoAfectacionIgv) ? "10" : origen.CodigoAfectacionIgv));
        }

        if (resultado.Count == 0)
        {
            throw new BusinessRuleException(
                "Selecciona al menos un ítem con cantidad mayor a cero para la nota de crédito.");
        }

        return resultado;
    }

    private static LineaResuelta DesdeDetalle(VentaDetalle d) =>
        new(
            d.ProductoId,
            d.CodigoSku,
            d.Descripcion,
            IgvCalculo.Round2(d.Cantidad),
            IgvCalculo.Round2(d.PrecioUnitario),
            IgvCalculo.Round2(d.ValorUnitario),
            IgvCalculo.Round2(d.Subtotal),
            IgvCalculo.Round2(d.Igv),
            IgvCalculo.Round2(d.Total),
            string.IsNullOrWhiteSpace(d.CodigoAfectacionIgv) ? "10" : d.CodigoAfectacionIgv);
}
