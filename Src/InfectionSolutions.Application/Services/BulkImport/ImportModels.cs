using System.Text;

namespace InfectionSolutions.Application.Services.BulkImport;

public enum ImportLogLevel
{
    Info,
    Warning,
    Error
}

/// <summary>Una linea del log: donde (hoja y fila de Excel), que campo y que paso.</summary>
public sealed record ImportLogEntry(ImportLogLevel Level, string Sheet, int? Row, string? Field, string Message);

/// <summary>Fila de datos tal como vino del Excel, con sus columnas ya identificadas.</summary>
public sealed record RawRow(int RowNumber, IReadOnlyDictionary<ImportField, object?> Values);

public sealed record RawSheet(string Name, IReadOnlyList<RawRow> Rows);

public sealed class ImportResult
{
    private readonly List<ImportLogEntry> _log = [];

    public string FileName { get; init; } = string.Empty;

    public int SheetsRead { get; set; }

    public int RowsRead { get; set; }

    public int ProductsCreated { get; set; }

    public int ProductsUpdated { get; set; }

    public int CustomersCreated { get; set; }

    public int CustomersUpdated { get; set; }

    public int SalesCreated { get; set; }

    /// <summary>false si la transaccion fallo y no se guardo nada.</summary>
    public bool Saved { get; set; }

    public IReadOnlyList<ImportLogEntry> Log => _log;

    public int ErrorCount => _log.Count(e => e.Level == ImportLogLevel.Error);

    public int WarningCount => _log.Count(e => e.Level == ImportLogLevel.Warning);

    public int TotalChanges => ProductsCreated + ProductsUpdated + CustomersCreated + CustomersUpdated + SalesCreated;

    public void Info(string sheet, int? row, string message, string? field = null) => _log.Add(new(ImportLogLevel.Info, sheet, row, field, message));

    public void Warning(string sheet, int? row, string message, string? field = null) => _log.Add(new(ImportLogLevel.Warning, sheet, row, field, message));

    public void Error(string sheet, int? row, string message, string? field = null) => _log.Add(new(ImportLogLevel.Error, sheet, row, field, message));

    public void ResetCounters()
    {
        ProductsCreated = ProductsUpdated = CustomersCreated = CustomersUpdated = SalesCreated = 0;
    }

    /// <summary>Log en CSV (separado por ;, que es lo que Excel en español abre directo).</summary>
    public string ToCsv()
    {
        var csv = new StringBuilder();
        csv.AppendLine("Nivel;Hoja;Fila;Campo;Mensaje");
        foreach (var entrada in _log)
        {
            csv.Append(Nivel(entrada.Level)).Append(';')
                .Append(Escapar(entrada.Sheet)).Append(';')
                .Append(entrada.Row?.ToString() ?? string.Empty).Append(';')
                .Append(Escapar(entrada.Field ?? string.Empty)).Append(';')
                .AppendLine(Escapar(entrada.Message));
        }

        return csv.ToString();
    }

    public static string Nivel(ImportLogLevel level) => level switch
    {
        ImportLogLevel.Error => "Error",
        ImportLogLevel.Warning => "Advertencia",
        _ => "Info"
    };

    private static string Escapar(string valor)
        => valor.IndexOfAny([';', '"', '\n', '\r']) >= 0 ? $"\"{valor.Replace("\"", "\"\"")}\"" : valor;
}
