using InfectionSolutions.Admin.ViewModels.Shared;
using InfectionSolutions.Application.Common;
using InfectionSolutions.Application.Dtos.Customers;

namespace InfectionSolutions.Admin.ViewModels.Customers;

public class CustomerIndexViewModel
{
    public required PagedResponse<CustomerResponse> Result { get; init; }

    public string? Q { get; init; }

    public bool OnlyActive { get; init; }

    public bool HasFilters => !string.IsNullOrWhiteSpace(Q) || OnlyActive;

    public PagerViewModel Pager => new()
    {
        Page = Result.Page,
        TotalPages = Result.TotalPages,
        TotalCount = Result.TotalCount,
        ItemName = "clientes",
        RouteValues = new Dictionary<string, string?>
        {
            ["q"] = Q,
            ["onlyActive"] = OnlyActive ? "true" : null
        }
    };
}
