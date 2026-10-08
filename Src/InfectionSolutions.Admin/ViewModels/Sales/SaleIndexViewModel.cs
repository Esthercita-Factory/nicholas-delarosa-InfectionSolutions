using InfectionSolutions.Admin.ViewModels.Shared;
using InfectionSolutions.Application.Common;
using InfectionSolutions.Application.Dtos.Sales;
using InfectionSolutions.Domain.Enums;

namespace InfectionSolutions.Admin.ViewModels.Sales;

public class SaleIndexViewModel
{
    public required PagedResponse<SaleListItemResponse> Result { get; init; }

    public string? Q { get; init; }

    public SaleStatus? Status { get; init; }

    public bool HasFilters => !string.IsNullOrWhiteSpace(Q) || Status.HasValue;

    public PagerViewModel Pager => new()
    {
        Page = Result.Page,
        TotalPages = Result.TotalPages,
        TotalCount = Result.TotalCount,
        ItemName = "ventas",
        RouteValues = new Dictionary<string, string?>
        {
            ["q"] = Q,
            ["status"] = Status?.ToString()
        }
    };
}
