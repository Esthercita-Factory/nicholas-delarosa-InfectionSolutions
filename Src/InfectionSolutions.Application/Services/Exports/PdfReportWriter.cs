using InfectionSolutions.Application.Common;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using static InfectionSolutions.Application.Common.PdfTheme;

namespace InfectionSolutions.Application.Services.Exports;

/// <summary>
/// Reporte tabular con la identidad de los recibos: titulo en Archivo
/// condensada, cinta metrica y renglones finos sin cebra, pensados para
/// leerse impresos.
/// </summary>
public static class PdfReportWriter
{
    private const float Margen = 32;

    public static byte[] Write(TabularReport report, CompanyOptions company, DateTimeOffset generatedAt)
        => Build(report, company, generatedAt).GeneratePdf();

    public static IDocument Build(TabularReport report, CompanyOptions company, DateTimeOffset generatedAt)
    {
        DocumentLibraries.EnsureConfigured();

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(report.Landscape ? PageSizes.A4.Landscape() : PageSizes.A4);
                page.Margin(0);
                page.PageColor(Blanco);
                page.DefaultTextStyle(Base.FontSize(8.5f));

                page.Header().PaddingHorizontal(Margen).PaddingTop(Margen).Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().AlignBottom().Text(report.Title).FontFamily(CondensedFont).FontSize(30).ExtraBold();
                        row.AutoItem().AlignBottom().PaddingBottom(4).Column(marca =>
                        {
                            marca.Item().AlignRight().Text(company.Name).FontFamily(CondensedFont).FontSize(12).Bold();
                            marca.Item().AlignRight().Text(DisplayFormats.DateTime(generatedAt)).FontSize(8).FontColor(Acero);
                        });
                    });

                    col.Item().PaddingTop(6).Element(c => Ruler(c));

                    col.Item().PaddingTop(8).PaddingBottom(14).Text(t =>
                    {
                        t.Span($"{report.Rows.Count:N0} {(report.Rows.Count == 1 ? "registro" : "registros")}").SemiBold();
                        if (report.Subtitle is not null)
                        {
                            t.Span($"    {report.Subtitle}").FontColor(Acero);
                        }
                    });
                });

                page.Content().PaddingHorizontal(Margen).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        foreach (var columna in report.Columns)
                        {
                            columns.RelativeColumn(columna.Width);
                        }
                    });

                    table.Header(header =>
                    {
                        foreach (var columna in report.Columns)
                        {
                            var celda = header.Cell().BorderBottom(1.2f).BorderColor(Grafito).PaddingBottom(5).PaddingHorizontal(7);
                            (AlineaDerecha(columna) ? celda.AlignRight() : celda)
                                .Text(columna.Header).FontSize(8).SemiBold().FontColor(Acero);
                        }
                    });

                    foreach (var fila in report.Rows)
                    {
                        for (var c = 0; c < report.Columns.Count; c++)
                        {
                            var columna = report.Columns[c];
                            var celda = table.Cell().BorderBottom(0.6f).BorderColor(Linea).PaddingVertical(5).PaddingHorizontal(7);
                            var texto = (AlineaDerecha(columna) ? celda.AlignRight() : celda).Text(Formatear(fila[c], columna.Type));

                            // La primera columna es la que identifica la fila.
                            if (c == 0)
                            {
                                texto.SemiBold();
                            }
                        }
                    }

                    if (report.Rows.Count == 0)
                    {
                        table.Cell().ColumnSpan((uint)report.Columns.Count).PaddingVertical(20).AlignCenter()
                            .Text("Ningún registro coincide con los filtros del listado.").FontColor(Acero);
                    }
                });

                page.Footer().PaddingHorizontal(Margen).PaddingBottom(20).PaddingTop(10).Column(col =>
                {
                    col.Item().Element(c => Ruler(c, Linea, 5));
                    col.Item().PaddingTop(5).Row(row =>
                    {
                        row.RelativeItem().Text($"{company.Name}, NIT {company.TaxId}").FontSize(7.5f).FontColor(Acero);
                        row.AutoItem().Text(t =>
                        {
                            t.DefaultTextStyle(x => x.FontSize(7.5f).FontColor(Acero));
                            t.CurrentPageNumber();
                            t.Span(" de ");
                            t.TotalPages();
                        });
                    });
                });
            });
        });
    }

    private static bool AlineaDerecha(ReportColumn columna) => columna.Type is ReportCellType.Money or ReportCellType.Integer;

    private static string Formatear(object? valor, ReportCellType tipo) => valor switch
    {
        null => "",
        decimal dinero when tipo == ReportCellType.Money => DisplayFormats.Money(dinero),
        int entero => entero.ToString("N0", DisplayFormats.Culture),
        DateTimeOffset fecha => DisplayFormats.DateTime(fecha),
        _ => Convert.ToString(valor, DisplayFormats.Culture) ?? string.Empty
    };
}
