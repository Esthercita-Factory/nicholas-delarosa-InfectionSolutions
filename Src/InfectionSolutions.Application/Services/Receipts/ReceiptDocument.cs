using InfectionSolutions.Application.Common;
using InfectionSolutions.Application.Dtos.Sales;
using InfectionSolutions.Domain.Enums;
using InfectionSolutions.Domain.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using static InfectionSolutions.Application.Common.PdfTheme;

namespace InfectionSolutions.Application.Services.Receipts;

/// <summary>
/// Recibo con forma de remision de despacho: banda de marca, cinta metrica,
/// renglones finos y el total en un bloque grafito. Una venta anulada lleva
/// un sello, como se marca a mano una remision en el mostrador.
/// </summary>
internal sealed class ReceiptDocument : IDocument
{
    private const float Margen = 40;

    private readonly SaleResponse _sale;
    private readonly CompanyOptions _company;

    public ReceiptDocument(SaleResponse sale, CompanyOptions company)
    {
        _sale = sale;
        _company = company;
    }

    private bool Anulada => _sale.Status == SaleStatus.Cancelled;

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"Recibo {_sale.SaleNumber}",
        Author = _company.Name,
        Subject = $"Recibo de venta {_sale.SaleNumber} para {_sale.CustomerName}"
    };

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(0);
            page.PageColor(Blanco);
            page.DefaultTextStyle(Base);

            page.Header().Element(ComposeHeader);
            page.Content().PaddingHorizontal(Margen).PaddingTop(22).Element(ComposeContent);
            page.Footer().PaddingHorizontal(Margen).PaddingBottom(26).Element(ComposeFooter);
        });
    }

    private void ComposeHeader(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().Background(Grafito).PaddingHorizontal(Margen).PaddingTop(30).PaddingBottom(22).Row(row =>
            {
                row.RelativeItem().Column(marca =>
                {
                    marca.Item().Text(_company.Name).FontFamily(CondensedFont).FontSize(24).Bold().FontColor(Blanco);
                    marca.Item().PaddingTop(1).Text(_company.Tagline).FontSize(9).FontColor(AceroClaro);
                });

                row.AutoItem().AlignBottom().Column(numero =>
                {
                    numero.Item().AlignRight().Text("Recibo de venta").FontSize(8.5f).FontColor(AceroClaro);
                    numero.Item().AlignRight().PaddingTop(3).Background(Bulto).PaddingHorizontal(8).PaddingVertical(2)
                        .Text(_sale.SaleNumber).FontFamily(CondensedFont).FontSize(20).ExtraBold().FontColor(Grafito);
                });
            });

            col.Item().PaddingHorizontal(Margen).PaddingTop(6).Element(c => Ruler(c, AceroClaro));
        });
    }

    private void ComposeContent(IContainer container)
    {
        container.Column(col =>
        {
            col.Spacing(22);

            col.Item().Row(row =>
            {
                row.RelativeItem(3).Column(cliente =>
                {
                    cliente.Item().Text("Despachado a").FontSize(8).FontColor(Acero);
                    cliente.Item().PaddingTop(2).Text(_sale.CustomerName).FontFamily(CondensedFont).FontSize(15).Bold();
                    cliente.Item().PaddingTop(4).Text($"Documento {_sale.CustomerDocument}");
                    cliente.Item().Text(_sale.CustomerEmail);
                    cliente.Item().Text(_sale.CustomerPhone);
                    if (!string.IsNullOrWhiteSpace(_sale.CustomerAddress))
                    {
                        cliente.Item().Text(_sale.CustomerAddress);
                    }
                });

                row.ConstantItem(24);

                row.RelativeItem(2).Column(venta =>
                {
                    Dato(venta, "Fecha", DisplayFormats.DateTime(_sale.SaleDate));
                    Dato(venta, "Estado", Anulada ? "Anulada" : "Completada", Anulada ? Rojo : Verde);
                    Dato(venta, "Productos", $"{_sale.Details.Count} {(_sale.Details.Count == 1 ? "referencia" : "referencias")}, {_sale.Details.Sum(d => d.Quantity)} unidades");

                    if (Anulada)
                    {
                        venta.Item().PaddingTop(6).PaddingLeft(62).Element(ComposeStamp);
                    }
                });
            });

            col.Item().Element(ComposeTable);

            col.Item().Row(row =>
            {
                row.RelativeItem().PaddingRight(28).Column(notas =>
                {
                    if (!string.IsNullOrWhiteSpace(_sale.Notes))
                    {
                        notas.Item().Text("Observaciones").FontSize(8).FontColor(Acero);
                        notas.Item().PaddingTop(2).PaddingBottom(10).Text(_sale.Notes);
                    }

                    notas.Item().Text("Revise el material al recibirlo. Para cambios o garantías presente este recibo.")
                        .FontSize(8).FontColor(Acero);
                });

                row.ConstantItem(220).Column(totales =>
                {
                    FilaTotal(totales, "Subtotal", _sale.Subtotal);
                    FilaTotal(totales, $"IVA {InventoryCalculator.TaxRate * 100:0} %", _sale.Tax);
                    totales.Item().PaddingTop(8).Background(Grafito).PaddingHorizontal(12).PaddingVertical(9).Row(r =>
                    {
                        r.RelativeItem().AlignMiddle().Text("Total").FontSize(10).SemiBold().FontColor(Blanco);
                        r.AutoItem().Text(DisplayFormats.Money(_sale.Total)).FontFamily(CondensedFont).FontSize(19).ExtraBold().FontColor(Bulto);
                    });
                });
            });
        });
    }

    private void ComposeTable(IContainer container)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(46);
                columns.RelativeColumn(4);
                columns.RelativeColumn(1.5f);
                columns.RelativeColumn(1.6f);
            });

            table.Header(header =>
            {
                Encabezado(header.Cell(), "Cant.", derecha: true);
                Encabezado(header.Cell(), "Producto", derecha: false, paddingLeft: 14);
                Encabezado(header.Cell(), "Precio unit.", derecha: true);
                Encabezado(header.Cell(), "Importe", derecha: true);
            });

            foreach (var linea in _sale.Details)
            {
                Celda(table.Cell()).AlignRight().Text(linea.Quantity.ToString("N0", DisplayFormats.Culture))
                    .FontFamily(CondensedFont).FontSize(12).SemiBold();
                Celda(table.Cell()).PaddingLeft(14).Column(producto =>
                {
                    producto.Item().Text(linea.ProductName).SemiBold();
                    producto.Item().Text($"{linea.Sku}, por {linea.Unit.ToLowerInvariant()}").FontSize(8).FontColor(Acero);
                });
                Celda(table.Cell()).AlignRight().Text(DisplayFormats.Money(linea.UnitPrice));
                Celda(table.Cell()).AlignRight().Text(DisplayFormats.Money(linea.LineTotal)).SemiBold();
            }
        });
    }

    private void ComposeFooter(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().Element(c => Ruler(c, Linea, 5));
            col.Item().PaddingTop(6).Row(row =>
            {
                var datos = new[] { $"NIT {_company.TaxId}", _company.Address, _company.Phone, _company.Email }
                    .Where(v => !string.IsNullOrWhiteSpace(v));
                row.RelativeItem().Text(string.Join("   ", datos)).FontSize(7.5f).FontColor(Acero);
                row.AutoItem().Text(t =>
                {
                    t.DefaultTextStyle(x => x.FontSize(7.5f).FontColor(Acero));
                    t.CurrentPageNumber();
                    t.Span(" de ");
                    t.TotalPages();
                });
            });
        });
    }

    /// <summary>Sello de anulada: recuadro rojo inclinado con la fecha, bajo los datos de la venta.</summary>
    private void ComposeStamp(IContainer container)
    {
        var fecha = _sale.CancelledAt is { } anulada ? DisplayFormats.Date(anulada) : string.Empty;

        container.AlignLeft().Width(132).Rotate(-4).Border(2f).BorderColor("#CCB3261E").Padding(6).Column(sello =>
        {
            sello.Item().AlignCenter().Text("ANULADA").FontFamily(CondensedFont).FontSize(22).ExtraBold().FontColor("#CCB3261E");
            if (fecha.Length > 0)
            {
                sello.Item().AlignCenter().Text(fecha).FontSize(9).SemiBold().FontColor("#CCB3261E");
            }
        });
    }

    private static void Dato(ColumnDescriptor columna, string etiqueta, string valor, string? color = null)
    {
        columna.Item().PaddingBottom(7).Row(row =>
        {
            row.ConstantItem(62).Text(etiqueta).FontSize(8).FontColor(Acero);
            row.RelativeItem().Text(valor).SemiBold().FontColor(color ?? Grafito);
        });
    }

    private static void FilaTotal(ColumnDescriptor columna, string etiqueta, decimal valor)
    {
        columna.Item().PaddingHorizontal(12).PaddingVertical(3).Row(r =>
        {
            r.RelativeItem().Text(etiqueta).FontColor(Acero);
            r.AutoItem().Text(DisplayFormats.Money(valor));
        });
    }

    private static void Encabezado(IContainer container, string texto, bool derecha, float paddingLeft = 0)
    {
        var celda = container.BorderBottom(1.2f).BorderColor(Grafito).PaddingBottom(5).PaddingLeft(paddingLeft);
        (derecha ? celda.AlignRight() : celda).Text(texto).FontSize(8).SemiBold().FontColor(Acero);
    }

    private static IContainer Celda(IContainer container)
        => container.BorderBottom(0.6f).BorderColor(Linea).PaddingVertical(7);
}
