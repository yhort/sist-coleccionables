using System.Globalization;
using CapitalPos.Tcg.Api.Contracts.Cpe;
using QRCoder;

namespace CapitalPos.Tcg.Api.Infrastructure.Cpe;

/// <summary>
/// QR de representación impresa SUNAT (RS 097-2012/SUNAT y modificatorias).
/// Campos: RUC|tipo|serie|número|IGV|total|fecha|tipo doc adquirente|nro doc|digest|
/// </summary>
public static class CpeQrSunat
{
    public static string Payload(EmitirCpeRequest request, string? hashFirma)
    {
        var fecha = request.FechaEmision.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var igv = request.TotalIgv.ToString("0.00", CultureInfo.InvariantCulture);
        var total = request.Total.ToString("0.00", CultureInfo.InvariantCulture);
        var digest = (hashFirma ?? string.Empty).Trim();
        return string.Join('|',
            request.Emisor.Ruc.Trim(),
            request.TipoComprobante.Trim(),
            request.Serie.Trim(),
            request.Correlativo.ToString(CultureInfo.InvariantCulture),
            igv,
            total,
            fecha,
            request.Cliente.TipoDocumento.Trim(),
            request.Cliente.NumeroDocumento.Trim(),
            digest,
            string.Empty);
    }

    public static byte[] Png(string payload, int pixelsPerModule = 8)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(data);
        return png.GetGraphic(pixelsPerModule);
    }

    public static string EtiquetaTipo(string tipoSunat) => tipoSunat switch
    {
        "01" => "FACTURA ELECTRÓNICA",
        "03" => "BOLETA DE VENTA ELECTRÓNICA",
        "07" => "NOTA DE CRÉDITO ELECTRÓNICA",
        "09" => "GUÍA DE REMISIÓN ELECTRÓNICA",
        "NV" => "NOTA DE VENTA",
        _ => "COMPROBANTE ELECTRÓNICO"
    };

    public static string EtiquetaDocumento(string tipoDocumento) => tipoDocumento switch
    {
        "1" => "DNI",
        "4" => "C.E.",
        "6" => "RUC",
        "7" => "PASAPORTE",
        _ => "DOC"
    };

    public static string LeyendaPrincipal(string tipoSunat) => tipoSunat switch
    {
        "NV" => "Documento interno. No es comprobante de pago electrónico SUNAT.",
        _ => $"Representación impresa de la {EtiquetaTipo(tipoSunat).ToLowerInvariant()}."
    };
}
