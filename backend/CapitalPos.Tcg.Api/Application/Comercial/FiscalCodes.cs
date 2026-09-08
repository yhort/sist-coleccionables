using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Application.Comercial;

public static class FiscalCodes
{
    public static string CodigoComprobante(TipoComprobanteSunat tipo) => tipo switch
    {
        TipoComprobanteSunat.FACTURA => "01",
        TipoComprobanteSunat.BOLETA => "03",
        TipoComprobanteSunat.NOTA_CREDITO => "07",
        TipoComprobanteSunat.GUIA_REMISION => "09",
        TipoComprobanteSunat.NOTA_VENTA => "NV",
        _ => "03"
    };

    public static string CodigoDocumento(TipoDocumentoIdentidad tipo) => tipo switch
    {
        TipoDocumentoIdentidad.DNI => "1",
        TipoDocumentoIdentidad.RUC => "6",
        TipoDocumentoIdentidad.CE => "4",
        TipoDocumentoIdentidad.PASAPORTE => "7",
        _ => "1"
    };

    public static string SeriePorDefecto(TipoComprobanteSunat tipo) => tipo switch
    {
        TipoComprobanteSunat.FACTURA => "F001",
        TipoComprobanteSunat.NOTA_CREDITO => "FC01",
        TipoComprobanteSunat.GUIA_REMISION => "T001",
        TipoComprobanteSunat.NOTA_VENTA => "NV01",
        _ => "B001"
    };

    /// <summary>
    /// capitalpos-cpe-api exige que la NC que modifica boleta inicie con B (BC01)
    /// y la que modifica factura inicie con F (FC01).
    /// </summary>
    public static string SerieNotaCredito(TipoComprobanteSunat afectado) =>
        afectado == TipoComprobanteSunat.FACTURA ? "FC01" : "BC01";

    public static string MontoEnLetras(decimal monto)
    {
        var total = IgvCalculo.Round2(monto);
        var enteros = (int)Math.Truncate(total);
        var centavos = (int)Math.Round((total - enteros) * 100m, 0, MidpointRounding.AwayFromZero);
        return $"{NumeroALetras(enteros)} Y {centavos:00}/100 SOLES";
    }

    private static string NumeroALetras(int valor)
    {
        if (valor == 0)
        {
            return "CERO";
        }

        if (valor < 0)
        {
            return "MENOS " + NumeroALetras(-valor);
        }

        var partes = new List<string>();
        var millones = valor / 1_000_000;
        var miles = (valor % 1_000_000) / 1000;
        var resto = valor % 1000;

        if (millones > 0)
        {
            partes.Add(millones == 1 ? "UN MILLON" : NumeroALetras(millones) + " MILLONES");
        }

        if (miles > 0)
        {
            partes.Add(miles == 1 ? "MIL" : NumeroALetras(miles) + " MIL");
        }

        if (resto > 0)
        {
            partes.Add(Centenas(resto));
        }

        return string.Join(' ', partes);
    }

    private static string Centenas(int valor)
    {
        if (valor == 100)
        {
            return "CIEN";
        }

        var centena = valor / 100;
        var resto = valor % 100;
        var prefijo = centena switch
        {
            0 => string.Empty,
            1 => "CIENTO",
            2 => "DOSCIENTOS",
            3 => "TRESCIENTOS",
            4 => "CUATROCIENTOS",
            5 => "QUINIENTOS",
            6 => "SEISCIENTOS",
            7 => "SETECIENTOS",
            8 => "OCHOCIENTOS",
            9 => "NOVECIENTOS",
            _ => string.Empty
        };

        var textoResto = resto == 0 ? string.Empty : Decenas(resto);
        return string.IsNullOrEmpty(prefijo)
            ? textoResto
            : string.IsNullOrEmpty(textoResto) ? prefijo : prefijo + " " + textoResto;
    }

    private static string Decenas(int valor)
    {
        if (valor < 10)
        {
            return Unidades(valor);
        }

        if (valor < 20)
        {
            return valor switch
            {
                10 => "DIEZ",
                11 => "ONCE",
                12 => "DOCE",
                13 => "TRECE",
                14 => "CATORCE",
                15 => "QUINCE",
                16 => "DIECISEIS",
                17 => "DIECISIETE",
                18 => "DIECIOCHO",
                19 => "DIECINUEVE",
                _ => Unidades(valor)
            };
        }

        var decena = valor / 10;
        var unidad = valor % 10;
        var raiz = decena switch
        {
            2 => "VEINTE",
            3 => "TREINTA",
            4 => "CUARENTA",
            5 => "CINCUENTA",
            6 => "SESENTA",
            7 => "SETENTA",
            8 => "OCHENTA",
            9 => "NOVENTA",
            _ => string.Empty
        };

        if (unidad == 0)
        {
            return raiz;
        }

        if (decena == 2)
        {
            return "VEINTI" + Unidades(unidad);
        }

        return raiz + " Y " + Unidades(unidad);
    }

    private static string Unidades(int valor) => valor switch
    {
        1 => "UN",
        2 => "DOS",
        3 => "TRES",
        4 => "CUATRO",
        5 => "CINCO",
        6 => "SEIS",
        7 => "SIETE",
        8 => "OCHO",
        9 => "NUEVE",
        _ => string.Empty
    };
}
