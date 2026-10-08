using InfectionSolutions.Application.Abstractions;
using InfectionSolutions.Application.Dtos.Dashboard;

namespace InfectionSolutions.Application.Services.Dashboard;

public interface IDashboardService
{
    Task<DashboardSummary> GetSummaryAsync(CancellationToken cancellationToken = default);
}

public sealed class DashboardService : IDashboardService
{
    /// <summary>Por debajo de este stock el producto aparece como alerta.</summary>
    public const int LowStockThreshold = 10;

    private readonly IDashboardQuery _query;
    private readonly IClock _clock;

    public DashboardService(IDashboardQuery query, IClock clock)
    {
        _query = query;
        _clock = clock;
    }

    public Task<DashboardSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var ahora = _clock.UtcNow;
        var inicioDeMes = new DateTimeOffset(ahora.Year, ahora.Month, 1, 0, 0, 0, TimeSpan.Zero);

        return _query.GetSummaryAsync(inicioDeMes, LowStockThreshold, cancellationToken);
    }
}
