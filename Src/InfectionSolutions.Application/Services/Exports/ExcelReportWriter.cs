using System.Drawing;
using InfectionSolutions.Application.Common;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using OfficeOpenXml.Table;

namespace InfectionSolutions.Application.Services.Exports;

public static class ExcelReportWriter
{
    /// <summary>Fila donde empiezan los encabezados: arriba van el titulo y la fecha.</summary>
    public const int HeaderRow = 4;

    public static byte[] Write(TabularReport report, DateTimeOffset generatedAt)
    {
        DocumentLibraries.EnsureConfigured();

        using var package = new ExcelPackage();
        var hoja = package.Workbook.Worksheets.Add(NombreDeHoja(report.Title));

        hoja.Cells[1, 1].Value = report.Title;
        hoja.Cells[1, 1].Style.Font.Bold = true;
        hoja.Cells[1, 1].Style.Font.Size = 14;
        hoja.Cells[2, 1].Value = $"Generado el {DisplayFormats.DateTime(generatedAt)}{(report.Subtitle is null ? "" : $". {report.Subtitle}")}";
        hoja.Cells[2, 1].Style.Font.Italic = true;
        hoja.Cells[2, 1].Style.Font.Color.SetColor(Color.FromArgb(107, 114, 128));

        for (var c = 0; c < report.Columns.Count; c++)
        {
            hoja.Cells[HeaderRow, c + 1].Value = report.Columns[c].Header;
        }

        for (var r = 0; r < report.Rows.Count; r++)
        {
            var fila = report.Rows[r];
            for (var c = 0; c < report.Columns.Count; c++)
            {
                var celda = hoja.Cells[HeaderRow + 1 + r, c + 1];
                celda.Value = fila[c] switch
                {
                    // Excel no tiene zona horaria: se escribe la hora de Colombia.
                    DateTimeOffset fecha => DisplayFormats.ToBusinessTime(fecha).DateTime,
                    var valor => valor
                };
            }
        }

        var ultimaFila = HeaderRow + Math.Max(report.Rows.Count, 1);
        for (var c = 0; c < report.Columns.Count; c++)
        {
            var columna = hoja.Cells[HeaderRow + 1, c + 1, ultimaFila, c + 1];
            switch (report.Columns[c].Type)
            {
                case ReportCellType.Money:
                    columna.Style.Numberformat.Format = "$ #,##0.00";
                    break;
                case ReportCellType.Integer:
                    columna.Style.Numberformat.Format = "#,##0";
                    break;
                case ReportCellType.DateTime:
                    columna.Style.Numberformat.Format = "dd/mm/yyyy hh:mm";
                    columna.Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                    break;
            }
        }

        // Tabla de Excel: filtros y bandas sin trabajo extra para quien lo abre.
        var rango = hoja.Cells[HeaderRow, 1, ultimaFila, report.Columns.Count];
        var tabla = hoja.Tables.Add(rango, NombreDeTabla(report.FileBaseName));
        tabla.TableStyle = TableStyles.Medium2;
        tabla.ShowFilter = true;

        hoja.Cells[HeaderRow, 1, ultimaFila, report.Columns.Count].AutoFitColumns(10, 60);
        hoja.View.FreezePanes(HeaderRow + 1, 1);

        return package.GetAsByteArray();
    }

    /// <summary>Excel limita el nombre de hoja a 31 caracteres y prohibe algunos simbolos.</summary>
    private static string NombreDeHoja(string titulo)
    {
        var limpio = new string(titulo.Where(ch => !"[]:*?/\\".Contains(ch)).ToArray());
        return limpio.Length <= 31 ? limpio : limpio[..31];
    }

    private static string NombreDeTabla(string baseName)
    {
        var limpio = new string(baseName.Where(char.IsLetterOrDigit).ToArray());
        return string.IsNullOrEmpty(limpio) ? "Datos" : $"T{limpio}";
    }
}
