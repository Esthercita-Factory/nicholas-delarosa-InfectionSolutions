using InfectionSolutions.Application.Dtos.Dashboard;
using InfectionSolutions.Application.Services.Dashboard;

namespace InfectionSolutions.Tests.Application;

public class DashboardServiceTests
{
    [Fact]
    public async Task GetSummaryAsync_ConsultaConFechasEnUtc()
    {
        // Npgsql rechaza timestamptz con offset distinto de cero.
        var query = Substitute.For<IDashboardQuery>();
        query.GetSummaryAsync(default, default, default).ReturnsForAnyArgs(new DashboardSummary());
        query.GetCompletedSalesSinceAsync(default, default).ReturnsForAnyArgs([]);
        var servicio = new DashboardService(query, new FixedClock(new DateTimeOffset(2026, 10, 8, 2, 0, 0, TimeSpan.Zero)));

        var resumen = await servicio.GetSummaryAsync();

        // 8 de octubre 02:00 UTC es todavia 7 de octubre en Colombia: el mes empieza el 1 a las 05:00 UTC.
        await query.Received(1).GetSummaryAsync(new DateTimeOffset(2026, 10, 1, 5, 0, 0, TimeSpan.Zero), DashboardService.LowStockThreshold, Arg.Any<CancellationToken>());
        await query.Received(1).GetCompletedSalesSinceAsync(Arg.Is<DateTimeOffset>(f => f.Offset == TimeSpan.Zero), Arg.Any<CancellationToken>());
        Assert.Equal(DashboardService.ChartDays, resumen.SalesByDay.Count);
        Assert.Equal(new DateOnly(2026, 10, 7), resumen.SalesByDay[^1].Day);
    }

    [Fact]
    public void AgruparPorDia_UsaElDiaDeColombiaYRellenaLosDiasSinVentas()
    {
        var ventas = new[]
        {
            // 7 de octubre 10:00 p. m. en Colombia = 8 de octubre 03:00 UTC: cuenta para el 7.
            new SalePoint(new DateTimeOffset(2026, 10, 8, 3, 0, 0, TimeSpan.Zero), 100m),
            new SalePoint(new DateTimeOffset(2026, 10, 7, 15, 0, 0, TimeSpan.Zero), 50m),
            new SalePoint(new DateTimeOffset(2026, 10, 5, 15, 0, 0, TimeSpan.Zero), 20m)
        };

        var dias = DashboardService.AgruparPorDia(ventas, new DateOnly(2026, 10, 5), 3);

        Assert.Equal(
            [
                new(new DateOnly(2026, 10, 5), 20m, 1),
                new(new DateOnly(2026, 10, 6), 0m, 0),
                new(new DateOnly(2026, 10, 7), 150m, 2)
            ],
            dias);
    }
}
