namespace InfectionSolutions.Application.Services.Exports;

public enum ExportFormat
{
    Excel,
    Pdf
}

public enum ReportCellType
{
    Text,
    Integer,
    Money,
    DateTime
}

public sealed record ReportColumn(string Header, ReportCellType Type = ReportCellType.Text, float Width = 1f);

/// <summary>
/// Tabla independiente del formato: la misma se escribe en Excel o en PDF.
/// Cada fila trae un valor por columna, en el mismo orden.
/// </summary>
public sealed class TabularReport
{
    public required string Title { get; init; }

    /// <summary>Texto bajo el titulo, p. ej. los filtros aplicados.</summary>
    public string? Subtitle { get; init; }

    public required IReadOnlyList<ReportColumn> Columns { get; init; }

    public required IReadOnlyList<object?[]> Rows { get; init; }

    /// <summary>Nombre del archivo sin extension.</summary>
    public required string FileBaseName { get; init; }

    /// <summary>Se dibuja apaisado cuando hay muchas columnas.</summary>
    public bool Landscape => Columns.Count > 6;
}

public sealed record ExportFile(byte[] Content, string ContentType, string FileName)
{
    public const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public const string PdfContentType = "application/pdf";
}
