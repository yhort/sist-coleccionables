using CapitalPos.Tcg.Api.Domain.Enums;
using System.Text.RegularExpressions;

namespace CapitalPos.Tcg.Api.Application.Comercial;

public static class DocumentoIdentidad
{
    public const string NombreClienteVarios = "CLIENTES VARIOS";
    public const string NombrePublicoGeneral = "PÚBLICO GENERAL";
    public const string NumeroSinDocumento = "00000000";

    public static bool EsPublicoGeneral(string? nombre, TipoDocumentoIdentidad tipo, string? numero)
    {
        if (tipo == TipoDocumentoIdentidad.SIN_DOCUMENTO)
        {
            return true;
        }

        var nro = SoloDigitos(numero);
        if (nro == NumeroSinDocumento)
        {
            return true;
        }

        var etiqueta = (nombre ?? string.Empty).Trim().ToUpperInvariant();
        return etiqueta is NombreClienteVarios or "PUBLICO GENERAL" or "PÚBLICO GENERAL";
    }

    public static (TipoDocumentoIdentidad Tipo, string Numero, string Nombre) NormalizarCliente(
        string? nombre,
        TipoDocumentoIdentidad? tipo,
        string? numero,
        bool esPublicoGeneral)
    {
        var nombreTrim = (nombre ?? string.Empty).Trim();
        if (esPublicoGeneral || tipo == TipoDocumentoIdentidad.SIN_DOCUMENTO)
        {
            return (
                TipoDocumentoIdentidad.SIN_DOCUMENTO,
                NumeroSinDocumento,
                nombreTrim.Length >= 2 ? nombreTrim : NombreClienteVarios);
        }

        if (nombreTrim.Length < 2)
        {
            throw new BusinessRuleException("Indica el nombre o razón social del cliente.");
        }

        var nro = (numero ?? string.Empty).Trim().ToUpperInvariant();
        var tipoFinal = tipo ?? Inferir(nro);
        if (tipoFinal == TipoDocumentoIdentidad.SIN_DOCUMENTO)
        {
            return (tipoFinal, esPublicoGeneral ? NumeroSinDocumento : string.Empty, nombreTrim);
        }

        Validar(tipoFinal, nro);
        return (tipoFinal, NormalizarNumero(tipoFinal, nro), nombreTrim);
    }

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
        string? numero)
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

    private static string NormalizarNumero(TipoDocumentoIdentidad tipo, string numero) => tipo switch
    {
        TipoDocumentoIdentidad.SIN_DOCUMENTO => NumeroSinDocumento,
        TipoDocumentoIdentidad.DNI or TipoDocumentoIdentidad.RUC => SoloDigitos(numero),
        _ => numero.Trim().ToUpperInvariant()
    };
}
