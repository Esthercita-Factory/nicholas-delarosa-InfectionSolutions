using InfectionSolutions.Application.Abstractions;
using InfectionSolutions.Application.Dtos.Dashboard;
using InfectionSolutions.Domain.Enums;
using InfectionSolutions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfectionSolutions.Infrastructure.Repositories;

/// <summary>Consultas de solo lectura para las metricas del panel.</summary>
public sealed class DashboardQuery : IDashboardQuery
{
    private const int RecentSalesCount = 5;
    private const int LowStockCount = 5;

    private readonly ApplicationDbContext _db;

    public DashboardQuery(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<DashboardSummary> GetSummaryAsync(DateTimeOffset monthStart, int lowStockThreshold, CancellationToken cancellationToken = default)
    {
        // Un DbContext no admite consultas en paralelo: van una tras otra.
        var totalProductos = await _db.Products.CountAsync(cancellationToken);
        var productosActivos = await _db.Products.CountAsync(p => p.IsActive, cancellationToken);
        var totalClientes = await _db.Customers.CountAsync(cancellationToken);
        var totalVentas = await _db.Sales.CountAsync(s => s.Status == SaleStatus.Completed, cancellationToken);

        var ventasDelMes = _db.Sales.Where(s => s.Status == SaleStatus.Completed && s.SaleDate >= monthStart);
        var cantidadDelMes = await ventasDelMes.CountAsync(cancellationToken);
        var ingresosDelMes = await ventasDelMes.SumAsync(s => (decimal?)s.Total, cancellationToken) ?? 0m;

        var recientes = await _db.Sales
            .AsNoTracking()
            .OrderByDescending(s => s.SaleDate)
            .Take(RecentSalesCount)
            .Select(s => new RecentSale(s.Id, s.SaleNumber, s.SaleDate, s.Customer.FullName, s.Total, s.Status == SaleStatus.Cancelled))
            .ToListAsync(cancellationToken);

        var stockBajo = await _db.Products
            .AsNoTracking()
            .Where(p => p.IsActive && p.Stock <= lowStockThreshold)
            .OrderBy(p => p.Stock)
            .ThenBy(p => p.Name)
            .Take(LowStockCount)
            .Select(p => new LowStockProduct(p.Id, p.Sku, p.Name, p.Stock))
            .ToListAsync(cancellationToken);

        return new DashboardSummary
        {
            TotalProducts = totalProductos,
            ActiveProducts = productosActivos,
            TotalCustomers = totalClientes,
            TotalSales = totalVentas,
            SalesThisMonth = cantidadDelMes,
            RevenueThisMonth = ingresosDelMes,
            RecentSales = recientes,
            LowStockProducts = stockBajo
        };
    }
}
