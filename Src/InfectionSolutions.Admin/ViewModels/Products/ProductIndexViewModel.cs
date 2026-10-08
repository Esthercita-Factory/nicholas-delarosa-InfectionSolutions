using InfectionSolutions.Admin.ViewModels.Shared;
using InfectionSolutions.Application.Common;
using InfectionSolutions.Application.Dtos.Products;

namespace InfectionSolutions.Admin.ViewModels.Products;

public class ProductIndexViewModel
{
    public required PagedResponse<ProductResponse> Result { get; init; }

    public string? Q { get; init; }

    public string? Category { get; init; }

    public bool OnlyActive { get; init; }

    public IReadOnlyList<string> Categories { get; init; } = [];

    public bool HasFilters => !string.IsNullOrWhiteSpace(Q) || !string.IsNullOrWhiteSpace(Category) || OnlyActive;

    public PagerViewModel Pager => new()
    {
        Page = Result.Page,
        TotalPages = Result.TotalPages,
        TotalCount = Result.TotalCount,
        ItemName = "productos",
        RouteValues = new Dictionary<string, string?>
        {
            ["q"] = Q,
            ["category"] = Category,
            ["onlyActive"] = OnlyActive ? "true" : null
        }
    };
}
