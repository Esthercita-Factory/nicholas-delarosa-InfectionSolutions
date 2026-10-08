using InfectionSolutions.Application.Abstractions;
using InfectionSolutions.Application.Common;
using InfectionSolutions.Domain.Entities;
using InfectionSolutions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfectionSolutions.Infrastructure.Repositories;

public sealed class ProductRepository : IProductRepository
{
    private readonly ApplicationDbContext _db;

    public ProductRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<Product>> ListAsync(ProductFilter filter, PageRequest page, CancellationToken cancellationToken = default)
    {
        var consulta = _db.Products.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Term))
        {
            var patron = $"%{filter.Term}%";
            consulta = consulta.Where(p =>
                EF.Functions.ILike(p.Name, patron)
                || EF.Functions.ILike(p.Sku, patron)
                || EF.Functions.ILike(p.Category, patron));
        }

        if (!string.IsNullOrWhiteSpace(filter.Category))
        {
            consulta = consulta.Where(p => p.Category == filter.Category);
        }

        if (filter.OnlyActive)
        {
            consulta = consulta.Where(p => p.IsActive);
        }

        var total = await consulta.CountAsync(cancellationToken);
        var items = await consulta
            .OrderByDescending(p => p.IsActive)
            .ThenBy(p => p.Name)
            .Skip(page.Skip)
            .Take(page.Take)
            .ToListAsync(cancellationToken);

        return new PagedResult<Product> { Items = items, TotalCount = total };
    }

    public Task<Product?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _db.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Product>> FindByIdsForUpdateAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var arreglo = ids.ToArray();

        return await _db.Products
            .FromSqlInterpolated($"SELECT * FROM products WHERE id = ANY ({arreglo}) FOR UPDATE")
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Product>> GetActiveAsync(CancellationToken cancellationToken = default)
        => await _db.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default)
        => await _db.Products
            .AsNoTracking()
            .Select(p => p.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(cancellationToken);

    public Task<bool> SkuExistsAsync(string sku, Guid? excludedId = null, CancellationToken cancellationToken = default)
        => _db.Products.AnyAsync(p => p.Sku == sku && (excludedId == null || p.Id != excludedId), cancellationToken);

    public Task<bool> HasSalesAsync(Guid id, CancellationToken cancellationToken = default)
        => _db.SaleDetails.AnyAsync(d => d.ProductId == id, cancellationToken);

    public async Task AddAsync(Product product, CancellationToken cancellationToken = default)
        => await _db.Products.AddAsync(product, cancellationToken);

    public void Remove(Product product) => _db.Products.Remove(product);
}
