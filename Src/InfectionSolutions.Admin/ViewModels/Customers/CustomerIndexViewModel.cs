using InfectionSolutions.Admin.ViewModels.Shared;
using InfectionSolutions.Application.Common;
using InfectionSolutions.Application.Dtos.Customers;

namespace InfectionSolutions.Admin.ViewModels.Customers;

public class CustomerIndexViewModel
{
    public required PagedResponse<CustomerResponse> Result { get; init; }

    public string? Q { get; init; }

    /// <summary>null = todos, true = solo activos, false = solo inactivos.</summary>
    public bool? Active { get; init; }

    public bool HasFilters => !string.IsNullOrWhiteSpace(Q) || Active.HasValue;

    public PagerViewModel Pager => new()
    {
        Page = Result.Page,
        TotalPages = Result.TotalPages,
        TotalCount = Result.TotalCount,
        ItemName = "clientes",
        RouteValues = new Dictionary<string, string?>
        {
            ["q"] = Q,
            ["active"] = Active?.ToString().ToLowerInvariant()
        }
    };
}
