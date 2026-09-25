using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Application.Cpe;

/// <summary>
/// Catálogo 09 SUNAT — Motivos de la Nota de Crédito.
/// </summary>
public static class Catalogo09Motivos
{
    public static readonly IReadOnlyDictionary<string, string> Descripciones =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["01"] = "Anulación de la operación",
            ["02"] = "Anulación por error en el RUC",
            ["03"] = "Corrección por error en la descripción",
            ["04"] = "Descuento global",
            ["05"] = "Descuento por ítem",
            ["06"] = "Devolución total",
            ["07"] = "Devolución por ítem",
            ["08"] = "Bonificación",
            ["09"] = "Disminución en el valor",
            ["10"] = "Otros Conceptos",
            ["11"] = "Ajustes de operaciones de exportación",
            ["12"] = "Ajustes afectos al IVAP",
            ["13"] = "Ajustes - montos y/o fechas de pago"
        };

    public static string NormalizarCodigo(string? codigo)
    {
        var valor = (codigo ?? string.Empty).Trim();
        if (valor.Length == 0)
        {
            return "01";
        }

        if (valor.Length == 1 && char.IsDigit(valor[0]))
        {
            valor = "0" + valor;
        }

        return valor;
    }

    public static string ResolverDescripcion(string? codigo, string? descripcion)
    {
        var codigoNorm = NormalizarCodigo(codigo);
        var texto = descripcion?.Trim() ?? string.Empty;
        if (texto.Length >= 3)
        {
            return texto;
        }

        return Descripciones.TryGetValue(codigoNorm, out var oficial)
            ? oficial
            : "Nota de crédito";
    }

    public static void Validar(string? codigo, string? descripcion)
    {
        var codigoNorm = NormalizarCodigo(codigo);
        if (!Descripciones.ContainsKey(codigoNorm))
        {
            throw new BusinessRuleException(
                $"El código de motivo '{codigoNorm}' no está en el catálogo 09 SUNAT (01–13).");
        }

        var texto = ResolverDescripcion(codigoNorm, descripcion);
        if (texto.Length < 3)
        {
            throw new BusinessRuleException("La nota de crédito exige una descripción de motivo (mínimo 3 caracteres).");
        }
    }

    /// <summary>
    /// 01/02 → Anulado; 06/07 (devoluciones) → Devuelto; resto → Anulado.
    /// </summary>
    public static EstadoPedidoDigital EstadoPedidoTrasMotivo(string? codigo)
    {
        var codigoNorm = NormalizarCodigo(codigo);
        return codigoNorm is "06" or "07"
            ? EstadoPedidoDigital.Devuelto
            : EstadoPedidoDigital.Anulado;
    }

    /// <summary>01 Anulación y 06 Devolución total (documento completo).</summary>
    public static bool EsDocumentoCompleto(string? codigo)
    {
        var codigoNorm = NormalizarCodigo(codigo);
        return codigoNorm is "01" or "02" or "06";
    }

    public static bool EsDevolucionPorItem(string? codigo) =>
        NormalizarCodigo(codigo) == "07";
}
