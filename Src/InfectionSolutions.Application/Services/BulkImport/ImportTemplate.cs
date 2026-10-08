using System.Drawing;
using InfectionSolutions.Application.Common;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace InfectionSolutions.Application.Services.BulkImport;

/// <summary>
/// Libro de ejemplo con datos desnormalizados a proposito: una hoja de ventas
/// que repite cliente y producto en cada fila, y una de inventario con otros
/// nombres de columna. Sirve de guia y de prueba del importador.
/// </summary>
internal static class ImportTemplate
{
    public static byte[] Build(DateTimeOffset generatedAt)
    {
        DocumentLibraries.EnsureConfigured();
        using var package = new ExcelPackage();

        var fecha = DisplayFormats.ToBusinessTime(generatedAt).Date.AddDays(-1);

        var ventas = package.Workbook.Worksheets.Add("Ventas");
        Titulo(ventas, "Ventas del mes (datos mezclados)", generatedAt);
        Tabla(ventas,
            ["Factura", "Fecha", "Cédula", "Cliente", "Correo", "Celular", "Edad", "Código", "Nombre", "Categoría", "Precio", "Cantidad"],
            [
                ["FAC-1001", fecha, "1020458796", "Laura Mendoza", "laura.mendoza@correo.com", "3001234567", 35, "CEM-50", "Cemento gris 50 kg", "Cementos", 32000m, 10],
                ["FAC-1001", fecha, "1020458796", "Laura Mendoza", "laura.mendoza@correo.com", "3001234567", 35, "VAR-12", "Varilla corrugada 1/2", "Aceros", 15500.5m, 25],
                ["FAC-1002", fecha, "900123456", "Constructora Andina SAS", "compras@andina.com.co", "6017459000", 30, "ARE-M3", "Arena lavada de río", "Agregados", 65000m, 3],
                ["FAC-1002", fecha, "900123456", "Constructora Andina SAS", "compras@andina.com.co", "6017459000", 30, "CEM-50", "Cemento gris 50 kg", "Cementos", 32000m, 40]
            ]);

        var inventario = package.Workbook.Worksheets.Add("Inventario");
        Titulo(inventario, "Inventario de bodega", generatedAt);
        Tabla(inventario,
            ["Ref", "Material", "Rubro", "UM", "Valor", "Existencias", "Detalle"],
            [
                ["CEM-50", "Cemento gris 50 kg", "Cementos", "Bulto", 32000m, 450, "Cemento de uso general"],
                ["VAR-12", "Varilla corrugada 1/2", "Aceros", "Unidad", 15500.5m, 800, "Varilla de 6 m"],
                ["ARE-M3", "Arena lavada de río", "Agregados", "Metro cúbico", 65000m, 60, null],
                ["LAD-T", "Ladrillo tolete", "Mampostería", "Unidad", 900m, 12000, null]
            ]);

        var ayuda = package.Workbook.Worksheets.Add("Instrucciones");
        string[] lineas =
        [
            "CARGA MASIVA · INSTRUCCIONES",
            "",
            "1. Puedes mezclar en una misma fila datos de producto, cliente y venta: el sistema los separa.",
            "2. Los nombres de columna son flexibles: Código, Ref o SKU; Cédula, NIT o Documento; Valor o Precio...",
            "3. Productos se identifican por su código y clientes por su documento: si ya existen se actualizan.",
            "4. Producto nuevo: código, nombre y precio obligatorios (categoría, unidad y stock tienen valor por defecto).",
            "5. Cliente nuevo: documento, nombre, correo, teléfono y edad obligatorios.",
            "6. Una fila con Cantidad es una línea de venta: necesita código de producto y documento del cliente.",
            "7. Las líneas con el mismo número de factura forman una venta; sin número, se agrupan por cliente y fecha.",
            "8. Las ventas importadas son históricas: no descuentan stock.",
            "9. Las filas con errores se omiten y quedan en el log; el resto se guarda.",
            "10. Esta hoja se ignora al importar."
        ];
        for (var i = 0; i < lineas.Length; i++)
        {
            ayuda.Cells[i + 1, 1].Value = lineas[i];
        }

        ayuda.Cells[1, 1].Style.Font.Bold = true;
        ayuda.Column(1).Width = 110;

        return package.GetAsByteArray();
    }

    private static void Titulo(ExcelWorksheet hoja, string titulo, DateTimeOffset generatedAt)
    {
        hoja.Cells[1, 1].Value = titulo;
        hoja.Cells[1, 1].Style.Font.Bold = true;
        hoja.Cells[1, 1].Style.Font.Size = 14;
        hoja.Cells[2, 1].Value = $"Generado el {DisplayFormats.DateTime(generatedAt)}";
        hoja.Cells[2, 1].Style.Font.Italic = true;
    }

    private static void Tabla(ExcelWorksheet hoja, string[] encabezados, object?[][] filas)
    {
        const int filaEncabezado = 4;
        for (var c = 0; c < encabezados.Length; c++)
        {
            var celda = hoja.Cells[filaEncabezado, c + 1];
            celda.Value = encabezados[c];
            celda.Style.Font.Bold = true;
            celda.Style.Font.Color.SetColor(Color.White);
            celda.Style.Fill.PatternType = ExcelFillStyle.Solid;
            celda.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(17, 24, 39));
        }

        for (var r = 0; r < filas.Length; r++)
        {
            for (var c = 0; c < filas[r].Length; c++)
            {
                var celda = hoja.Cells[filaEncabezado + 1 + r, c + 1];
                celda.Value = filas[r][c];
                if (filas[r][c] is DateTime)
                {
                    celda.Style.Numberformat.Format = "dd/mm/yyyy";
                }
                else if (filas[r][c] is decimal)
                {
                    celda.Style.Numberformat.Format = "#,##0.00";
                }
            }
        }

        hoja.Cells[filaEncabezado, 1, filaEncabezado + filas.Length, encabezados.Length].AutoFitColumns(10, 40);
    }
}
