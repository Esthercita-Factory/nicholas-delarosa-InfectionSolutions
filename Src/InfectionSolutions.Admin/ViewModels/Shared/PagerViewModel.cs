namespace InfectionSolutions.Admin.ViewModels.Shared;

/// <summary>Datos de _Pager: la pagina actual y los filtros a conservar en cada enlace.</summary>
public class PagerViewModel
{
    public int Page { get; init; }

    public int TotalPages { get; init; }

    public int TotalCount { get; init; }

    public string ItemName { get; init; } = "registros";

    public IDictionary<string, string?> RouteValues { get; init; } = new Dictionary<string, string?>();

    public IDictionary<string, string?> RouteFor(int page)
    {
        return new Dictionary<string, string?>(RouteValues) { ["page"] = page.ToString() };
    }
}
