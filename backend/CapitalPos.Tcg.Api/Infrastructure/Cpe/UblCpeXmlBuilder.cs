using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using CapitalPos.Tcg.Api.Contracts.Cpe;

namespace CapitalPos.Tcg.Api.Infrastructure.Cpe;

/// <summary>
/// Genera UBL 2.1 (Invoice / CreditNote) con extensión de firma local (digest SHA-256).
/// El envío a SUNAT/OSE lo hace <see cref="ICpeEmisor"/>.
/// </summary>
public static class UblCpeXmlBuilder
{
    private static readonly XNamespace InvoiceNs = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
    private static readonly XNamespace CreditNs = "urn:oasis:names:specification:ubl:schema:xsd:CreditNote-2";
    private static readonly XNamespace Cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    private static readonly XNamespace Cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    private static readonly XNamespace Ext = "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2";
    private static readonly XNamespace Ds = "http://www.w3.org/2000/09/xmldsig#";

    public static (string Xml, string HashFirma) Construir(EmitirCpeRequest request)
    {
        var esNota = request.TipoComprobante == "07";
        var rootName = esNota ? CreditNs + "CreditNote" : InvoiceNs + "Invoice";
        var id = $"{request.Serie}-{request.Correlativo:00000000}";
        var fecha = request.FechaEmision.ToString("yyyy-MM-dd");
        var hora = request.FechaEmision.ToString("HH:mm:ss");

        var documento = new XElement(
            rootName,
            new XAttribute(XNamespace.Xmlns + "cac", Cac),
            new XAttribute(XNamespace.Xmlns + "cbc", Cbc),
            new XAttribute(XNamespace.Xmlns + "ext", Ext),
            new XAttribute(XNamespace.Xmlns + "ds", Ds),
            new XElement(Ext + "UBLExtensions",
                new XElement(Ext + "UBLExtension",
                    new XElement(Ext + "ExtensionContent",
                        new XElement(Ds + "Signature", new XAttribute("Id", "SignatureSP"),
                            new XElement(Ds + "SignedInfo",
                                new XElement(Ds + "CanonicalizationMethod",
                                    new XAttribute("Algorithm", "http://www.w3.org/2001/10/xml-exc-c14n#")),
                                new XElement(Ds + "SignatureMethod",
                                    new XAttribute("Algorithm", "http://www.w3.org/2000/09/xmldsig#rsa-sha256")),
                                new XElement(Ds + "Reference", new XAttribute("URI", ""),
                                    new XElement(Ds + "DigestMethod",
                                        new XAttribute("Algorithm", "http://www.w3.org/2001/04/xmlenc#sha256")),
                                    new XElement(Ds + "DigestValue", "PENDIENTE"))))))),
            new XElement(Cbc + "UBLVersionID", "2.1"),
            new XElement(Cbc + "CustomizationID", "2.0"),
            new XElement(Cbc + "ID", id),
            new XElement(Cbc + "IssueDate", fecha),
            new XElement(Cbc + "IssueTime", hora),
            new XElement(Cbc + "InvoiceTypeCode",
                new XAttribute("listID", request.TipoOperacion),
                request.TipoComprobante),
            new XElement(Cbc + "DocumentCurrencyCode", request.Moneda),
            DocumentoReferencia(request),
            FirmaReferencia(request),
            Parte(Cac + "AccountingSupplierParty", request),
            Parte(Cac + "AccountingCustomerParty", request),
            new XElement(Cac + "PaymentTerms",
                new XElement(Cbc + "ID", "FormaPago"),
                new XElement(Cbc + "PaymentMeansID", request.FormaPago)),
            Impuesto(request),
            Monetario(request),
            Lineas(request, esNota));

        if (esNota)
        {
            documento.Element(Cbc + "InvoiceTypeCode")!.Name = Cbc + "CreditNoteTypeCode";
        }

        var xmlSinDigest = documento.ToString(SaveOptions.DisableFormatting);
        var hash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(xmlSinDigest)));
        var digest = documento.Descendants(Ds + "DigestValue").First();
        digest.Value = hash;

        var xml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" + Environment.NewLine + documento.ToString(SaveOptions.None);
        return (xml, hash);
    }

    public static string CdrAceptacion(EmitirCpeRequest request, string hashFirma)
    {
        var id = $"{request.Serie}-{request.Correlativo:00000000}";
        return
            $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <ApplicationResponse xmlns="urn:oasis:names:specification:ubl:schema:xsd:ApplicationResponse-2"
              xmlns:cbc="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"
              xmlns:cac="urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2">
              <cbc:ID>CDR-{id}</cbc:ID>
              <cbc:IssueDate>{DateTime.UtcNow:yyyy-MM-dd}</cbc:IssueDate>
              <cac:DocumentResponse>
                <cac:Response>
                  <cbc:ResponseCode>0</cbc:ResponseCode>
                  <cbc:Description>La Factura numero {id} ha sido aceptada</cbc:Description>
                </cac:Response>
                <cac:DocumentReference>
                  <cbc:ID>{id}</cbc:ID>
                  <cbc:DocumentTypeCode>{request.TipoComprobante}</cbc:DocumentTypeCode>
                </cac:DocumentReference>
              </cac:DocumentResponse>
              <cbc:Note>Hash {hashFirma}</cbc:Note>
            </ApplicationResponse>
            """;
    }

    private static XElement FirmaReferencia(EmitirCpeRequest request) =>
        new(Cac + "Signature",
            new XElement(Cbc + "ID", "IDSignSP"),
            new XElement(Cac + "SignatoryParty",
                new XElement(Cac + "PartyIdentification",
                    new XElement(Cbc + "ID", request.RucEmisor)),
                new XElement(Cac + "PartyName",
                    new XElement(Cbc + "Name", request.Emisor.RazonSocial))),
            new XElement(Cac + "DigitalSignatureAttachment",
                new XElement(Cac + "ExternalReference",
                    new XElement(Cbc + "URI", "#SignatureSP"))));

    private static XElement? DocumentoReferencia(EmitirCpeRequest request)
    {
        if (request.DocumentoReferencia is null)
        {
            return null;
        }

        return new XElement(Cac + "BillingReference",
            new XElement(Cac + "InvoiceDocumentReference",
                new XElement(Cbc + "ID", request.DocumentoReferencia.SerieCorrelativo),
                new XElement(Cbc + "DocumentTypeCode", request.DocumentoReferencia.TipoComprobante)));
    }

    private static XElement Parte(XName name, EmitirCpeRequest request)
    {
        var esEmisor = name == Cac + "AccountingSupplierParty";
        var id = esEmisor ? request.Emisor.Ruc : request.Cliente.NumeroDocumento;
        var scheme = esEmisor ? "6" : request.Cliente.TipoDocumento;
        var razon = esEmisor ? request.Emisor.RazonSocial : request.Cliente.RazonSocial;

        var party = new XElement(Cac + "Party",
            new XElement(Cac + "PartyIdentification",
                new XElement(Cbc + "ID", new XAttribute("schemeID", scheme), id)),
            new XElement(Cac + "PartyLegalEntity",
                new XElement(Cbc + "RegistrationName", new XCData(razon))));

        if (esEmisor)
        {
            party.Add(
                new XElement(Cac + "PartyName",
                    new XElement(Cbc + "Name", new XCData(request.Emisor.NombreComercial))),
                new XElement(Cac + "PostalAddress",
                    new XElement(Cbc + "ID", request.Emisor.Ubigeo),
                    new XElement(Cbc + "StreetName", new XCData(request.Emisor.Direccion)),
                    new XElement(Cbc + "CityName", request.Emisor.Provincia),
                    new XElement(Cbc + "CountrySubentity", request.Emisor.Departamento),
                    new XElement(Cbc + "District", request.Emisor.Distrito),
                    new XElement(Cac + "Country",
                        new XElement(Cbc + "IdentificationCode", "PE"))));
        }

        return new XElement(name, party);
    }

    private static XElement Impuesto(EmitirCpeRequest request) =>
        new(Cac + "TaxTotal",
            new XElement(Cbc + "TaxAmount",
                new XAttribute("currencyID", request.Moneda),
                Dec(request.TotalIgv)),
            new XElement(Cac + "TaxSubtotal",
                new XElement(Cbc + "TaxableAmount",
                    new XAttribute("currencyID", request.Moneda),
                    Dec(request.TotalGravada)),
                new XElement(Cbc + "TaxAmount",
                    new XAttribute("currencyID", request.Moneda),
                    Dec(request.TotalIgv)),
                new XElement(Cac + "TaxCategory",
                    new XElement(Cac + "TaxScheme",
                        new XElement(Cbc + "ID", "1000"),
                        new XElement(Cbc + "Name", "IGV"),
                        new XElement(Cbc + "TaxTypeCode", "VAT")))));

    private static XElement Monetario(EmitirCpeRequest request) =>
        new(Cac + "LegalMonetaryTotal",
            new XElement(Cbc + "LineExtensionAmount",
                new XAttribute("currencyID", request.Moneda),
                Dec(request.TotalGravada)),
            new XElement(Cbc + "TaxInclusiveAmount",
                new XAttribute("currencyID", request.Moneda),
                Dec(request.Total)),
            new XElement(Cbc + "PayableAmount",
                new XAttribute("currencyID", request.Moneda),
                Dec(request.Total)));

    private static IEnumerable<XElement> Lineas(EmitirCpeRequest request, bool esNota)
    {
        var lineName = esNota ? Cac + "CreditNoteLine" : Cac + "InvoiceLine";
        var qtyName = esNota ? Cbc + "CreditedQuantity" : Cbc + "InvoicedQuantity";
        var i = 1;
        foreach (var item in request.Items)
        {
            yield return new XElement(lineName,
                new XElement(Cbc + "ID", i++),
                new XElement(qtyName,
                    new XAttribute("unitCode", item.UnidadMedida),
                    Dec(item.Cantidad)),
                new XElement(Cbc + "LineExtensionAmount",
                    new XAttribute("currencyID", request.Moneda),
                    Dec(item.Subtotal)),
                new XElement(Cac + "PricingReference",
                    new XElement(Cac + "AlternativeConditionPrice",
                        new XElement(Cbc + "PriceAmount",
                            new XAttribute("currencyID", request.Moneda),
                            Dec(item.PrecioUnitario)),
                        new XElement(Cbc + "PriceTypeCode", "01"))),
                new XElement(Cac + "TaxTotal",
                    new XElement(Cbc + "TaxAmount",
                        new XAttribute("currencyID", request.Moneda),
                        Dec(item.Igv)),
                    new XElement(Cac + "TaxSubtotal",
                        new XElement(Cbc + "TaxableAmount",
                            new XAttribute("currencyID", request.Moneda),
                            Dec(item.Subtotal)),
                        new XElement(Cbc + "TaxAmount",
                            new XAttribute("currencyID", request.Moneda),
                            Dec(item.Igv)),
                        new XElement(Cac + "TaxCategory",
                            new XElement(Cbc + "Percent", "18.00"),
                            new XElement(Cbc + "TaxExemptionReasonCode", item.CodigoAfectacionIgv),
                            new XElement(Cac + "TaxScheme",
                                new XElement(Cbc + "ID", "1000"),
                                new XElement(Cbc + "Name", "IGV"),
                                new XElement(Cbc + "TaxTypeCode", "VAT"))))),
                new XElement(Cac + "Item",
                    new XElement(Cbc + "Description", new XCData(item.Descripcion)),
                    new XElement(Cac + "SellersItemIdentification",
                        new XElement(Cbc + "ID", item.Codigo))),
                new XElement(Cac + "Price",
                    new XElement(Cbc + "PriceAmount",
                        new XAttribute("currencyID", request.Moneda),
                        Dec(item.ValorUnitario))));
        }
    }

    private static string Dec(decimal valor) =>
        valor.ToString("0.00", CultureInfo.InvariantCulture);
}
