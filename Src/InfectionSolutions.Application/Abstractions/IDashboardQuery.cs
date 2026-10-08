using InfectionSolutions.Application.Dtos.Dashboard;

namespace InfectionSolutions.Application.Abstractions;

public interface IDashboardQuery
{
    Task<DashboardSummary> GetSummaryAsync(DateTimeOffset monthStart, int lowStockThreshold, CancellationToken cancellationToken = default);
}
