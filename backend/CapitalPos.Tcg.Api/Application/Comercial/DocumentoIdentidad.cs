using CapitalPos.Tcg.Api.Domain.Enums;
using System.Text.RegularExpressions;

namespace CapitalPos.Tcg.Api.Application.Comercial;

public static class DocumentoIdentidad
{
    public const string NombreClienteVarios = "CLIENTES VARIOS";
    public const string NombrePublicoGeneral = "PÚBLICO GENERAL";

    /// <summary>
    /// Valor legado usado históricamente para «sin documento» / cliente varios.
    /// Ya no se persiste; se reconoce solo para migraciones y datos antiguos.
    /// </summary>
    public const string NumeroSinDocumentoLegado = "00000000";

    /// <summary>Alias de compatibilidad; preferir <see cref="NumeroSinDocumentoLegado"/>.</summary>
    public const string NumeroSinDocumento = NumeroSinDocumentoLegado;

    /// <summary>Catálogo 06 / guía de llenado SUNAT: adquirente no identificado.</summary>
    public const string CodigoSunatSinDocumento = "-";
    public const string NumeroSunatSinDocumento = "-";

    /// <summary>SUNAT no exige identificar al adquirente en boletas de este importe o menor.</summary>
    public const decimal UmbralIdentificacionBoleta = 700m;

    public static bool EsPublicoGeneral(string? nombre, TipoDocumentoIdentidad tipo, string? numero)
    {
        var etiqueta = (nombre ?? string.Empty).Trim().ToUpperInvariant();
        if (etiqueta is NombreClienteVarios or "PUBLICO GENERAL" or "PÚBLICO GENERAL")
        {
            return true;
        }

        // Legado: registros antiguos con 00000000 / 0000000 y sin nombre.
        return tipo == TipoDocumentoIdentidad.SIN_DOCUMENTO
            && EsNumeroLegadoSinDocumento(numero)
            && string.IsNullOrWhiteSpace(nombre);
    }

    public static bool EstaIdentificado(TipoDocumentoIdentidad tipo, string? numero)
    {
        if (tipo == TipoDocumentoIdentidad.SIN_DOCUMENTO)
        {
            return false;
        }

        var nro = (numero ?? string.Empty).Trim();
        return !string.IsNullOrEmpty(nro)
            && nro != NumeroSunatSinDocumento
            && !EsNumeroLegadoSinDocumento(nro);
    }

    public static (string TipoCodigo, string Numero) ReceptorSunat(
        TipoDocumentoIdentidad tipo,
        string? numero)
    {
        if (!EstaIdentificado(tipo, numero))
        {
            return (CodigoSunatSinDocumento, NumeroSunatSinDocumento);
        }

        var codigo = FiscalCodes.CodigoDocumento(tipo);
        var nro = tipo is TipoDocumentoIdentidad.DNI or TipoDocumentoIdentidad.RUC
            ? SoloDigitos(numero)
            : (numero ?? string.Empty).Trim();
        return (codigo, nro);
    }

    /// <summary>
    /// Normaliza datos de cliente. Sin documento / público general → Numero = null (persistir NULL).
    /// </summary>
    public static (TipoDocumentoIdentidad Tipo, string? Numero, string Nombre) NormalizarCliente(
        string? nombre,
        TipoDocumentoIdentidad? tipo,
        string? numero,
        bool esPublicoGeneral)
    {
        var nombreTrim = (nombre ?? string.Empty).Trim();
        if (esPublicoGeneral)
        {
            return (
                TipoDocumentoIdentidad.SIN_DOCUMENTO,
                null,
                nombreTrim.Length >= 2 ? nombreTrim : NombreClienteVarios);
        }

        if (nombreTrim.Length < 2)
        {
            throw new BusinessRuleException("Indica el nombre o razón social del cliente.");
        }

        var nro = (numero ?? string.Empty).Trim().ToUpperInvariant();
        var tipoFinal = tipo ?? Inferir(nro);
        if (EsNumeroAusente(nro) && tipoFinal != TipoDocumentoIdentidad.RUC)
        {
            return (TipoDocumentoIdentidad.SIN_DOCUMENTO, null, nombreTrim);
        }

        if (tipoFinal == TipoDocumentoIdentidad.SIN_DOCUMENTO)
        {
            return (tipoFinal, null, nombreTrim);
        }

        Validar(tipoFinal, nro);
        return (tipoFinal, NormalizarNumero(tipoFinal, nro), nombreTrim);
    }

    /// <summary>Convierte vacío / legado 00000000 / 0000000 a null para persistencia.</summary>
    public static string? NumeroParaPersistir(string? numero) =>
        EsNumeroAusente(numero ?? string.Empty) ? null : (numero ?? string.Empty).Trim();

    public static void Validar(TipoDocumentoIdentidad tipo, string? numero)
    {
        var nro = (numero ?? string.Empty).Trim();
        switch (tipo)
        {
            case TipoDocumentoIdentidad.SIN_DOCUMENTO:
                return;
            case TipoDocumentoIdentidad.DNI:
                if (SoloDigitos(nro).Length != 8)
                {
                    throw new BusinessRuleException("El DNI debe tener 8 dígitos.");
                }

                break;
            case TipoDocumentoIdentidad.RUC:
                if (SoloDigitos(nro).Length != 11)
                {
                    throw new BusinessRuleException("El RUC debe tener 11 dígitos.");
                }

                break;
            case TipoDocumentoIdentidad.CE:
                if (nro.Length is < 8 or > 12)
                {
                    throw new BusinessRuleException("El carné de extranjería debe tener entre 8 y 12 caracteres.");
                }

                break;
            case TipoDocumentoIdentidad.PASAPORTE:
                if (nro.Length is < 6 or > 12)
                {
                    throw new BusinessRuleException("El pasaporte debe tener entre 6 y 12 caracteres.");
                }

                break;
            default:
                throw new BusinessRuleException("Tipo de documento no soportado.");
        }
    }

    public static void ValidarComprobante(
        TipoComprobanteSunat tipoComprobante,
        TipoDocumentoIdentidad tipoDocumento,
        string? numero,
        decimal total = 0)
    {
        if (tipoComprobante == TipoComprobanteSunat.FACTURA)
        {
            if (tipoDocumento != TipoDocumentoIdentidad.RUC || SoloDigitos(numero).Length != 11)
            {
                throw new BusinessRuleException("La factura exige un cliente con RUC de 11 dígitos.");
            }

            return;
        }

        if (tipoComprobante is TipoComprobanteSunat.BOLETA or TipoComprobanteSunat.NOTA_VENTA
            && tipoDocumento == TipoDocumentoIdentidad.RUC)
        {
            throw new BusinessRuleException("La boleta se emite con DNI o sin documento. Usa factura para un RUC.");
        }

        if (tipoComprobante == TipoComprobanteSunat.BOLETA
            && total > UmbralIdentificacionBoleta
            && !EstaIdentificado(tipoDocumento, numero))
        {
            throw new BusinessRuleException(
                "La boleta mayor a S/ 700 exige DNI u otro documento de identidad del adquirente.");
        }
    }

    public static string ValidarRucProveedor(string? ruc)
    {
        var nro = SoloDigitos(ruc);
        if (nro.Length != 11)
        {
            throw new BusinessRuleException("El RUC del proveedor debe tener 11 dígitos.");
        }

        return nro;
    }

    public static string SoloDigitos(string? valor) =>
        Regex.Replace(valor ?? string.Empty, @"\D", string.Empty);

    private static TipoDocumentoIdentidad Inferir(string numero)
    {
        var digitos = SoloDigitos(numero);
        return digitos.Length switch
        {
            11 => TipoDocumentoIdentidad.RUC,
            8 => TipoDocumentoIdentidad.DNI,
            0 => TipoDocumentoIdentidad.SIN_DOCUMENTO,
            _ => TipoDocumentoIdentidad.DNI
        };
    }

    private static bool EsNumeroAusente(string numero) =>
        string.IsNullOrWhiteSpace(numero)
        || numero == NumeroSunatSinDocumento
        || EsNumeroLegadoSinDocumento(numero);

    /// <summary>Valores históricos que significaban «sin documento» (8 o 7 ceros).</summary>
    public static bool EsNumeroLegadoSinDocumento(string? numero)
    {
        var digitos = SoloDigitos(numero);
        return digitos is "00000000" or "0000000";
    }

    private static string NormalizarNumero(TipoDocumentoIdentidad tipo, string numero) => tipo switch
    {
        TipoDocumentoIdentidad.SIN_DOCUMENTO => string.Empty,
        TipoDocumentoIdentidad.DNI or TipoDocumentoIdentidad.RUC => SoloDigitos(numero),
        _ => numero.Trim().ToUpperInvariant()
    };
}
