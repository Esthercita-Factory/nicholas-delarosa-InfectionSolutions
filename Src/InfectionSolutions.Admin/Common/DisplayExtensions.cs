using InfectionSolutions.Domain.Enums;

namespace InfectionSolutions.Admin.Common;

public static class DisplayExtensions
{
    public static string ToDisplay(this SaleStatus status) => status switch
    {
        SaleStatus.Completed => "Completada",
        SaleStatus.Cancelled => "Anulada",
        _ => status.ToString()
    };

    public static string ToBadgeClass(this SaleStatus status) => status switch
    {
        SaleStatus.Completed => "text-bg-success",
        SaleStatus.Cancelled => "text-bg-secondary",
        _ => "text-bg-light"
    };
}
