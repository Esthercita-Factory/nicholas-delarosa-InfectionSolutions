namespace InfectionSolutions.Application.Dtos.Dashboard;

public sealed record DashboardSummary
{
    public int TotalProducts { get; init; }

    public int ActiveProducts { get; init; }

    public int TotalCustomers { get; init; }

    public int TotalSales { get; init; }

    /// <summary>Total facturado (ventas no anuladas) en el mes actual.</summary>
    public decimal RevenueThisMonth { get; init; }

    public int SalesThisMonth { get; init; }

    public IReadOnlyList<RecentSale> RecentSales { get; init; } = [];

    public IReadOnlyList<LowStockProduct> LowStockProducts { get; init; } = [];

    /// <summary>Ingresos por dia de las ultimas dos semanas (hora de Colombia), en orden.</summary>
    public IReadOnlyList<DailySales> SalesByDay { get; init; } = [];
}

public sealed record DailySales(DateOnly Day, decimal Total, int Count);

public sealed record RecentSale(Guid Id, string SaleNumber, DateTimeOffset SaleDate, string CustomerName, decimal Total, bool IsCancelled);

public sealed record LowStockProduct(Guid Id, string Sku, string Name, int Stock);
