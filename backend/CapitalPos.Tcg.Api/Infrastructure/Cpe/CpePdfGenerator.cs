using System.Globalization;
using CapitalPos.Tcg.Api.Application.Cpe;
using CapitalPos.Tcg.Api.Contracts.Cpe;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CapitalPos.Tcg.Api.Infrastructure.Cpe;

public sealed class CpePdfGenerator : ICpePdfGenerator
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-PE");
    private static readonly Lazy<byte[]?> LogoBytes = new(CargarLogo);
    private const string LeyendaAutorizacion =
        "Autorizado mediante Resolución de Superintendencia N.° 097-2012/SUNAT y normas modificatorias.";
    private const string LeyendaConsulta = "Consulte el comprobante en www.sunat.gob.pe";

    static CpePdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Generar(EmitirCpeRequest payload, string? hashFirma, CpePdfFormato formato)
    {
        var qr = CpeQrSunat.Png(CpeQrSunat.Payload(payload, hashFirma), formato == CpePdfFormato.Ticket ? 6 : 8);
        var documento = formato == CpePdfFormato.Ticket
            ? Document.Create(container => ComponerTicket(container, payload, hashFirma, qr))
            : Document.Create(container => ComponerA4(container, payload, hashFirma, qr));
        return documento.GeneratePdf();
    }

    private static void ComponerA4(
        IDocumentContainer container,
        EmitirCpeRequest payload,
        string? hashFirma,
        byte[] qr)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(18, Unit.Millimetre);
            page.DefaultTextStyle(t => t.FontSize(9).FontColor(Colors.Grey.Darken4));
            page.Content().Column(col =>
            {
                col.Spacing(10);
                col.Item().Element(c => EncabezadoA4(c, payload));
                col.Item().Element(c => BloqueCliente(c, payload, compacto: false));
                if (!string.IsNullOrWhiteSpace(payload.DocumentoReferencia?.SerieCorrelativo))
                {
                    col.Item().Text($"Documento que modifica: {payload.DocumentoReferencia!.TipoComprobante} {payload.DocumentoReferencia.SerieCorrelativo}")
                        .FontSize(8);
                }

                col.Item().Element(c => TablaItemsA4(c, payload));
                col.Item().Element(c => TotalesYQrA4(c, payload, hashFirma, qr));
                col.Item().Element(c => Leyendas(c, payload, hashFirma, compacto: false));
            });
        });
    }

    private static void ComponerTicket(
        IDocumentContainer container,
        EmitirCpeRequest payload,
        string? hashFirma,
        byte[] qr)
    {
        container.Page(page =>
        {
            page.ContinuousSize(80, Unit.Millimetre);
            page.Margin(4, Unit.Millimetre);
            page.DefaultTextStyle(t => t.FontSize(7.5f).FontColor(Colors.Black));
            page.Content().Column(col =>
            {
                col.Spacing(4);
                if (LogoBytes.Value is { Length: > 0 } logo)
                {
                    col.Item().AlignCenter().Height(18, Unit.Millimetre).Image(logo).FitArea();
                }

                col.Item().AlignCenter().Text(payload.Emisor.NombreComercial).Bold().FontSize(10);
                col.Item().AlignCenter().Text(payload.Emisor.RazonSocial).FontSize(7);
                col.Item().AlignCenter().Text($"RUC {payload.Emisor.Ruc}").Bold();
                col.Item().AlignCenter().Text(DireccionEmisor(payload)).FontSize(6.5f);
                col.Item().PaddingVertical(2).LineHorizontal(0.6f);
                col.Item().AlignCenter().Text(CpeQrSunat.EtiquetaTipo(payload.TipoComprobante)).Bold().FontSize(8);
                col.Item().AlignCenter().Text(Numero(payload)).Bold().FontSize(11);
                col.Item().AlignCenter().Text($"Fecha: {payload.FechaEmision:dd/MM/yyyy HH:mm}");
                col.Item().PaddingVertical(2).LineHorizontal(0.4f);
                col.Item().Element(c => BloqueCliente(c, payload, compacto: true));
                if (!string.IsNullOrWhiteSpace(payload.DocumentoReferencia?.SerieCorrelativo))
                {
                    col.Item().Text($"Ref. {payload.DocumentoReferencia!.TipoComprobante} {payload.DocumentoReferencia.SerieCorrelativo}");
                }

                col.Item().PaddingVertical(2).LineHorizontal(0.4f);
                col.Item().Element(c => ItemsTicket(c, payload));
                col.Item().PaddingVertical(2).LineHorizontal(0.4f);
                col.Item().Element(c => TotalesTicket(c, payload));
                col.Item().Text($"SON: {payload.MontoEnLetras}").FontSize(6.5f);
                col.Item().AlignCenter().Width(32, Unit.Millimetre).Image(qr);
                col.Item().Element(c => Leyendas(c, payload, hashFirma, compacto: true));
            });
        });
    }

    private static void EncabezadoA4(IContainer container, EmitirCpeRequest payload)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Spacing(4);
                col.Item().Row(marca =>
                {
                    if (LogoBytes.Value is { Length: > 0 } logo)
                    {
                        marca.ConstantItem(52).Height(22, Unit.Millimetre).Image(logo).FitArea();
                        marca.ConstantItem(8);
                    }

                    marca.RelativeItem().AlignMiddle().Column(texto =>
                    {
                        texto.Item().Text(payload.Emisor.NombreComercial).Bold().FontSize(16).FontColor(Colors.Blue.Darken3);
                        texto.Item().Text(payload.Emisor.RazonSocial).FontSize(10);
                    });
                });
                col.Item().PaddingTop(2).Text(DireccionEmisor(payload));
                col.Item().Text($"{payload.Emisor.Distrito} — {payload.Emisor.Provincia} — {payload.Emisor.Departamento}");
                col.Item().Text($"Ubigeo {payload.Emisor.Ubigeo}");
            });
            row.ConstantItem(190).Border(1).BorderColor(Colors.Blue.Darken2).Padding(10).Column(col =>
            {
                col.Item().AlignCenter().Text($"RUC {payload.Emisor.Ruc}").Bold().FontSize(11);
                col.Item().PaddingTop(6).AlignCenter().Text(CpeQrSunat.EtiquetaTipo(payload.TipoComprobante))
                    .Bold().FontSize(9).FontColor(Colors.Blue.Darken3);
                col.Item().PaddingTop(6).AlignCenter().Text(Numero(payload)).Bold().FontSize(14);
                col.Item().PaddingTop(4).AlignCenter().Text($"Fecha: {payload.FechaEmision:dd/MM/yyyy}").FontSize(8);
                col.Item().AlignCenter().Text($"{payload.FormaPago} · {payload.Moneda}").FontSize(8);
            });
        });
    }

    private static void BloqueCliente(IContainer container, EmitirCpeRequest payload, bool compacto)
    {
        var doc = $"{CpeQrSunat.EtiquetaDocumento(payload.Cliente.TipoDocumento)} {payload.Cliente.NumeroDocumento}";
        if (compacto)
        {
            container.Column(col =>
            {
                col.Item().Text($"Cliente: {payload.Cliente.RazonSocial}").Bold();
                col.Item().Text(doc);
            });
            return;
        }

        container.Border(0.6f).BorderColor(Colors.Grey.Lighten1).Padding(8).Column(col =>
        {
            col.Spacing(2);
            col.Item().Text("Adquirente / Usuario").Bold().FontSize(8).FontColor(Colors.Grey.Darken1);
            col.Item().Text(payload.Cliente.RazonSocial).Bold().FontSize(11);
            col.Item().Text(doc);
        });
    }

    private static void TablaItemsA4(IContainer container, EmitirCpeRequest payload)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.ConstantColumn(28);
                cols.RelativeColumn(4);
                cols.ConstantColumn(42);
                cols.ConstantColumn(52);
                cols.ConstantColumn(52);
                cols.ConstantColumn(58);
            });
            table.Header(header =>
            {
                header.Cell().Element(CeldaCabecera).Text("Cant.");
                header.Cell().Element(CeldaCabecera).Text("Descripción");
                header.Cell().Element(CeldaCabecera).AlignRight().Text("V. unit.");
                header.Cell().Element(CeldaCabecera).AlignRight().Text("P. unit.");
                header.Cell().Element(CeldaCabecera).AlignRight().Text("IGV");
                header.Cell().Element(CeldaCabecera).AlignRight().Text("Total");
            });
            foreach (var item in payload.Items)
            {
                table.Cell().Element(CeldaCuerpo).Text(Cantidad(item.Cantidad));
                table.Cell().Element(CeldaCuerpo).Text($"{item.Codigo}  {item.Descripcion}");
                table.Cell().Element(CeldaCuerpo).AlignRight().Text(Moneda(item.ValorUnitario));
                table.Cell().Element(CeldaCuerpo).AlignRight().Text(Moneda(item.PrecioUnitario));
                table.Cell().Element(CeldaCuerpo).AlignRight().Text(Moneda(item.Igv));
                table.Cell().Element(CeldaCuerpo).AlignRight().Text(Moneda(item.Total));
            }
        });
    }

    private static void ItemsTicket(IContainer container, EmitirCpeRequest payload)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Text("Item").Bold();
                row.ConstantItem(28).AlignRight().Text("Cant").Bold();
                row.ConstantItem(36).AlignRight().Text("Total").Bold();
            });
            foreach (var item in payload.Items)
            {
                col.Item().Text(item.Descripcion);
                col.Item().Row(row =>
                {
                    row.RelativeItem().Text($"{Cantidad(item.Cantidad)} x {Moneda(item.PrecioUnitario)}");
                    row.ConstantItem(36).AlignRight().Text(Moneda(item.Total));
                });
            }
        });
    }

    private static void TotalesYQrA4(
        IContainer container,
        EmitirCpeRequest payload,
        string? hashFirma,
        byte[] qr)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Spacing(4);
                col.Item().Width(38, Unit.Millimetre).Image(qr);
                col.Item().Text($"Hash: {hashFirma ?? "—"}").FontSize(7);
                col.Item().Text($"SON: {payload.MontoEnLetras}").Italic().FontSize(8);
            });
            row.ConstantItem(210).Column(col =>
            {
                col.Item().Element(c => LineaTotal(c, "Op. gravada", payload.TotalGravada));
                col.Item().Element(c => LineaTotal(c, "Op. exonerada", payload.TotalExonerada));
                col.Item().Element(c => LineaTotal(c, "Op. inafecta", payload.TotalInafecta));
                col.Item().Element(c => LineaTotal(c, "IGV (18%)", payload.TotalIgv));
                col.Item().PaddingTop(4).Element(c => LineaTotal(c, "Importe total", payload.Total, resaltar: true));
            });
        });
    }

    private static void TotalesTicket(IContainer container, EmitirCpeRequest payload)
    {
        container.Column(col =>
        {
            col.Item().Element(c => LineaTotal(c, "Gravada", payload.TotalGravada));
            if (payload.TotalExonerada > 0)
            {
                col.Item().Element(c => LineaTotal(c, "Exonerada", payload.TotalExonerada));
            }

            if (payload.TotalInafecta > 0)
            {
                col.Item().Element(c => LineaTotal(c, "Inafecta", payload.TotalInafecta));
            }

            col.Item().Element(c => LineaTotal(c, "IGV", payload.TotalIgv));
            col.Item().Element(c => LineaTotal(c, "TOTAL", payload.Total, resaltar: true));
        });
    }

    private static void Leyendas(IContainer container, EmitirCpeRequest payload, string? hashFirma, bool compacto)
    {
        container.Column(col =>
        {
            col.Spacing(compacto ? 2 : 3);
            col.Item().Text(CpeQrSunat.LeyendaPrincipal(payload.TipoComprobante))
                .FontSize(compacto ? 6 : 8).Italic();
            if (payload.TipoComprobante != "NV")
            {
                col.Item().Text(LeyendaAutorizacion).FontSize(compacto ? 5.5f : 7);
                col.Item().Text(LeyendaConsulta).FontSize(compacto ? 6 : 7.5f);
            }
            else
            {
                col.Item().Text("Descuenta stock. Se regulariza con boleta consolidada diaria.")
                    .FontSize(compacto ? 5.5f : 7);
            }
            if (!compacto && !string.IsNullOrWhiteSpace(hashFirma))
            {
                col.Item().Text($"Código hash: {hashFirma}").FontSize(7);
            }
        });
    }

    private static void LineaTotal(IContainer container, string etiqueta, decimal monto, bool resaltar = false)
    {
        container.Row(row =>
        {
            if (resaltar)
            {
                row.RelativeItem().Text(etiqueta).Bold();
                row.ConstantItem(70).AlignRight().Text(Moneda(monto)).Bold();
            }
            else
            {
                row.RelativeItem().Text(etiqueta);
                row.ConstantItem(70).AlignRight().Text(Moneda(monto));
            }
        });
    }

    private static IContainer CeldaCabecera(IContainer container) =>
        container.Background(Colors.Blue.Darken3).Padding(4).DefaultTextStyle(t => t.FontColor(Colors.White).SemiBold().FontSize(8));

    private static IContainer CeldaCuerpo(IContainer container) =>
        container.BorderBottom(0.4f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4).PaddingHorizontal(3);

    private static string DireccionEmisor(EmitirCpeRequest payload) =>
        $"{payload.Emisor.Direccion}, {payload.Emisor.Distrito}";

    private static string Numero(EmitirCpeRequest payload) =>
        $"{payload.Serie}-{payload.Correlativo}";

    private static string Moneda(decimal valor) => valor.ToString("C2", Cultura);

    private static string Cantidad(decimal valor) =>
        valor == decimal.Truncate(valor)
            ? valor.ToString("0", Cultura)
            : valor.ToString("0.###", Cultura);

    private static byte[]? CargarLogo()
    {
        try
        {
            var candidatos = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "Assets", "logo-trunqi.jpg"),
                Path.Combine(Directory.GetCurrentDirectory(), "Assets", "logo-trunqi.jpg"),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Assets", "logo-trunqi.jpg")),
            };
            foreach (var ruta in candidatos)
            {
                if (File.Exists(ruta))
                {
                    return File.ReadAllBytes(ruta);
                }
            }
        }
        catch
        {
            /* sin logo: el PDF sigue siendo válido */
        }

        return null;
    }
}
