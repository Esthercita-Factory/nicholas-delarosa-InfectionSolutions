using InfectionSolutions.Application.Abstractions;
using InfectionSolutions.Application.Common;
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

    /// <summary>Dias que muestra la grafica de ventas, incluido hoy.</summary>
    public const int ChartDays = 14;

    private readonly IDashboardQuery _query;
    private readonly IClock _clock;

    public DashboardService(IDashboardQuery query, IClock clock)
    {
        _query = query;
        _clock = clock;
    }

    public async Task<DashboardSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        // Mes y dias se cuentan en hora de Colombia; a la base van en UTC
        // (Npgsql rechaza timestamptz con un offset distinto de cero).
        var hoy = DisplayFormats.ToBusinessTime(_clock.UtcNow);
        var inicioDeMes = new DateTimeOffset(hoy.Year, hoy.Month, 1, 0, 0, 0, DisplayFormats.BusinessOffset);
        var primerDia = new DateTimeOffset(hoy.Date.AddDays(-(ChartDays - 1)), DisplayFormats.BusinessOffset);

        var resumen = await _query.GetSummaryAsync(inicioDeMes.ToUniversalTime(), LowStockThreshold, cancellationToken);
        var ventas = await _query.GetCompletedSalesSinceAsync(primerDia.ToUniversalTime(), cancellationToken);

        return resumen with { SalesByDay = AgruparPorDia(ventas, DateOnly.FromDateTime(primerDia.DateTime), ChartDays) };
    }

    /// <summary>Un punto por dia, incluidos los dias sin ventas (en cero).</summary>
    public static IReadOnlyList<DailySales> AgruparPorDia(IEnumerable<SalePoint> ventas, DateOnly desde, int dias)
    {
        var porDia = ventas
            .GroupBy(v => DateOnly.FromDateTime(DisplayFormats.ToBusinessTime(v.SaleDate).DateTime))
            .ToDictionary(g => g.Key, g => (Total: g.Sum(v => v.Total), Cantidad: g.Count()));

        return Enumerable.Range(0, dias)
            .Select(i => desde.AddDays(i))
            .Select(dia => porDia.TryGetValue(dia, out var datos)
                ? new DailySales(dia, datos.Total, datos.Cantidad)
                : new DailySales(dia, 0m, 0))
            .ToList();
    }
}
