using InfectionSolutions.Application.Dtos.Dashboard;

namespace InfectionSolutions.Application.Abstractions;

/// <summary>Venta completada reducida a lo que necesita la grafica: cuando y cuanto.</summary>
public sealed record SalePoint(DateTimeOffset SaleDate, decimal Total);

public interface IDashboardQuery
{
    Task<DashboardSummary> GetSummaryAsync(DateTimeOffset monthStart, int lowStockThreshold, CancellationToken cancellationToken = default);

    /// <summary>Ventas completadas desde esa fecha (para agruparlas por dia).</summary>
    Task<IReadOnlyList<SalePoint>> GetCompletedSalesSinceAsync(DateTimeOffset from, CancellationToken cancellationToken = default);
}
