using InfectionSolutions.Application.Common;
using OfficeOpenXml;

namespace InfectionSolutions.Application.Services.BulkImport;

/// <summary>
/// Lee el libro con EPPlus y lo deja como filas crudas con sus columnas
/// identificadas. No valida ni toca la base: eso es del normalizador.
/// </summary>
public static class ExcelImportReader
{
    /// <summary>Filas que se revisan buscando los encabezados (puede haber titulo y fecha antes).</summary>
    private const int FilasParaBuscarEncabezado = 10;

    /// <summary>Con menos columnas reconocidas se asume que la hoja no es una tabla de datos.</summary>
    private const int MinimoColumnasReconocidas = 2;

    public const int MaxRowsPerSheet = 10_000;

    public static IReadOnlyList<RawSheet> Read(Stream stream, ImportResult result)
    {
        DocumentLibraries.EnsureConfigured();

        using var package = new ExcelPackage();
        try
        {
            package.Load(stream);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or InvalidOperationException or ArgumentException)
        {
            result.Error("Archivo", null, "El archivo no es un Excel (.xlsx) válido o está dañado.");
            return [];
        }

        var hojas = new List<RawSheet>();
        foreach (var hoja in package.Workbook.Worksheets)
        {
            if (EsHojaInformativa(hoja.Name))
            {
                result.Info(hoja.Name, null, "Hoja omitida: es de instrucciones.");
                continue;
            }

            if (hoja.Dimension is null)
            {
                result.Info(hoja.Name, null, "Hoja omitida: está vacía.");
                continue;
            }

            var leida = LeerHoja(hoja, result);
            if (leida is not null)
            {
                hojas.Add(leida);
            }
        }

        result.SheetsRead = hojas.Count;
        if (hojas.Count == 0 && result.ErrorCount == 0)
        {
            result.Error("Archivo", null, "No se encontró ninguna hoja con columnas reconocibles (por ejemplo Código, Producto, Precio, Documento, Cliente, Cantidad).");
        }

        return hojas;
    }

    private static RawSheet? LeerHoja(ExcelWorksheet hoja, ImportResult result)
    {
        var primeraColumna = hoja.Dimension.Start.Column;
        var ultimaColumna = hoja.Dimension.End.Column;
        var ultimaFila = hoja.Dimension.End.Row;

        var (filaEncabezado, campos) = BuscarEncabezado(hoja, primeraColumna, ultimaColumna, ultimaFila);
        if (filaEncabezado is null)
        {
            result.Warning(hoja.Name, null, "Hoja omitida: no se reconocieron sus columnas.");
            return null;
        }

        // Columnas utiles: la primera de cada campo. Las repetidas o desconocidas se reportan.
        var columnas = new Dictionary<int, ImportField>();
        var usados = new HashSet<ImportField>();
        for (var i = 0; i < campos.Count; i++)
        {
            var columna = primeraColumna + i;
            var encabezado = hoja.Cells[filaEncabezado.Value, columna].Text;
            if (string.IsNullOrWhiteSpace(encabezado))
            {
                continue;
            }

            if (campos[i] is not { } campo)
            {
                result.Info(hoja.Name, filaEncabezado, $"Columna '{encabezado.Trim()}' ignorada: no corresponde a ningún dato conocido.");
            }
            else if (!usados.Add(campo))
            {
                result.Warning(hoja.Name, filaEncabezado, $"Columna '{encabezado.Trim()}' ignorada: el dato {ColumnAliases.Label(campo)} ya viene en otra columna.");
            }
            else
            {
                columnas[columna] = campo;
            }
        }

        var reconocidas = string.Join(", ", columnas.Values.Select(ColumnAliases.Label));
        result.Info(hoja.Name, filaEncabezado, $"Encabezados en la fila {filaEncabezado}. Columnas reconocidas: {reconocidas}.");

        var filas = new List<RawRow>();
        var hasta = Math.Min(ultimaFila, filaEncabezado.Value + MaxRowsPerSheet);
        for (var fila = filaEncabezado.Value + 1; fila <= hasta; fila++)
        {
            var valores = new Dictionary<ImportField, object?>();
            foreach (var (columna, campo) in columnas)
            {
                var valor = hoja.Cells[fila, columna].Value;
                if (valor is not null && ImportValueParser.Text(valor) is not null)
                {
                    valores[campo] = valor is string texto ? texto.Trim() : valor;
                }
            }

            if (valores.Count > 0)
            {
                filas.Add(new RawRow(fila, valores));
            }
        }

        if (ultimaFila > hasta)
        {
            result.Warning(hoja.Name, null, $"La hoja tiene más de {MaxRowsPerSheet:N0} filas: solo se procesaron las primeras.");
        }

        return new RawSheet(hoja.Name, filas);
    }

    /// <summary>La fila (entre las primeras) con mas columnas reconocidas.</summary>
    private static (int? Fila, IReadOnlyList<ImportField?> Campos) BuscarEncabezado(ExcelWorksheet hoja, int primeraColumna, int ultimaColumna, int ultimaFila)
    {
        int? mejorFila = null;
        IReadOnlyList<ImportField?> mejoresCampos = [];
        var mejorPuntaje = 0;

        var primeraFila = hoja.Dimension.Start.Row;
        for (var fila = primeraFila; fila <= Math.Min(ultimaFila, primeraFila + FilasParaBuscarEncabezado - 1); fila++)
        {
            var encabezados = Enumerable.Range(primeraColumna, ultimaColumna - primeraColumna + 1)
                .Select(columna => (string?)hoja.Cells[fila, columna].Text)
                .ToList();

            var campos = HeaderMatcher.MatchAll(encabezados);
            var puntaje = campos.Where(c => c.HasValue).Distinct().Count();
            if (puntaje > mejorPuntaje)
            {
                mejorPuntaje = puntaje;
                mejorFila = fila;
                mejoresCampos = campos;
            }
        }

        return mejorPuntaje >= MinimoColumnasReconocidas ? (mejorFila, mejoresCampos) : (null, []);
    }

    private static bool EsHojaInformativa(string nombre)
    {
        var normalizado = HeaderMatcher.Normalize(nombre);
        return normalizado.Contains("instruccion") || normalizado.Contains("ayuda") || normalizado.Contains("leeme") || normalizado.Contains("readme");
    }
}
