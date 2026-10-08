using System.Globalization;
using System.Text;

namespace InfectionSolutions.Application.Services.BulkImport;

public static class HeaderMatcher
{
    /// <summary>"Código (SKU)" -> "codigosku"; "Teléfono Cliente" -> "telefonocliente".</summary>
    public static string Normalize(string? header)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            return string.Empty;
        }

        var descompuesto = header.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var limpio = new StringBuilder(descompuesto.Length);
        foreach (var ch in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(ch))
            {
                limpio.Append(ch);
            }
        }

        return limpio.ToString();
    }

    /// <summary>
    /// Asigna un campo a cada encabezado de la hoja. "Nombre" a secas es
    /// ambiguo: se decide segun las demas columnas. Devuelve null en las
    /// columnas que no se reconocen.
    /// </summary>
    public static IReadOnlyList<ImportField?> MatchAll(IReadOnlyList<string?> headers)
    {
        var normalizados = headers.Select(Normalize).ToList();
        var directos = normalizados
            .Select(h => ColumnAliases.Map.TryGetValue(h, out var campo) ? campo : (ImportField?)null)
            .ToList();

        var campoDeNombre = ResolverNombre(directos);

        return normalizados
            .Select((h, i) => directos[i] ?? (h == "nombre" ? campoDeNombre : null))
            .ToList();
    }

    private static ImportField ResolverNombre(IReadOnlyList<ImportField?> conocidos)
    {
        // Si ya hay una columna de cliente, "Nombre" es el producto, y al reves.
        if (conocidos.Contains(ImportField.CustomerName))
        {
            return ImportField.ProductName;
        }

        if (conocidos.Contains(ImportField.ProductName))
        {
            return ImportField.CustomerName;
        }

        var hayProducto = conocidos.Any(c => c is ImportField.Sku or ImportField.Price or ImportField.Stock);
        var hayCliente = conocidos.Any(c => c is ImportField.Document or ImportField.Email or ImportField.Age);

        return hayCliente && !hayProducto ? ImportField.CustomerName : ImportField.ProductName;
    }
}
