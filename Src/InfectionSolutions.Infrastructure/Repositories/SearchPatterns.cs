namespace InfectionSolutions.Infrastructure.Repositories;

/// <summary>
/// Patrones para ILIKE. Las busquedas comparan con unaccent(...) a ambos lados,
/// asi que no distinguen mayusculas ni tildes: "cemento", "CEMENTO" y "cémento"
/// encuentran lo mismo.
/// </summary>
internal static class SearchPatterns
{
    /// <summary>"%texto%" con % y _ escapados: lo que escribe el usuario no son comodines.</summary>
    public static string Contains(string term) => $"%{Escape(term.Trim())}%";

    /// <summary>Igualdad sin mayusculas ni tildes (ILIKE sin comodines).</summary>
    public static string Exact(string value) => Escape(value.Trim());

    private static string Escape(string value)
        => value.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
}
