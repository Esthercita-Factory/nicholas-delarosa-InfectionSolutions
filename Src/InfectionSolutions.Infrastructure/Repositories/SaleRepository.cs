using InfectionSolutions.Application.Abstractions;
using InfectionSolutions.Application.Common;
using InfectionSolutions.Domain.Entities;
using InfectionSolutions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfectionSolutions.Infrastructure.Repositories;

public sealed class SaleRepository : ISaleRepository
{
    private readonly ApplicationDbContext _db;

    public SaleRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<Sale>> ListAsync(SaleFilter filter, PageRequest page, CancellationToken cancellationToken = default)
    {
        var consulta = Filtrar(filter);

        var total = await consulta.CountAsync(cancellationToken);
        var items = await consulta
            .OrderByDescending(s => s.SaleDate)
            .Skip(page.Skip)
            .Take(page.Take)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return new PagedResult<Sale> { Items = items, TotalCount = total };
    }

    public async Task<IReadOnlyList<Sale>> ListAllAsync(SaleFilter filter, CancellationToken cancellationToken = default)
        => await Filtrar(filter)
            .OrderByDescending(s => s.SaleDate)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

    public Task<Sale?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _db.Sales
            .Include(s => s.Customer)
            .Include(s => s.Details)
                .ThenInclude(d => d.Product)
            .AsSplitQuery()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<int> CountByNumberPrefixAsync(string prefix, CancellationToken cancellationToken = default)
        => _db.Sales.CountAsync(s => s.SaleNumber.StartsWith(prefix), cancellationToken);

    public async Task<IReadOnlyList<string>> FindExistingNumbersAsync(IReadOnlyCollection<string> saleNumbers, CancellationToken cancellationToken = default)
    {
        if (saleNumbers.Count == 0)
        {
            return [];
        }

        return await _db.Sales
            .AsNoTracking()
            .Where(s => saleNumbers.Contains(s.SaleNumber))
            .Select(s => s.SaleNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Sale sale, CancellationToken cancellationToken = default)
        => await _db.Sales.AddAsync(sale, cancellationToken);

    /// <summary>Incluye cliente y detalles: el listado muestra cuantos productos tiene cada venta.</summary>
    private IQueryable<Sale> Filtrar(SaleFilter filter)
    {
        var consulta = _db.Sales
            .AsNoTracking()
            .Include(s => s.Customer)
            .Include(s => s.Details)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Term))
        {
            var patron = SearchPatterns.Contains(filter.Term);
            consulta = consulta.Where(s =>
                EF.Functions.ILike(s.SaleNumber, patron)
                || EF.Functions.ILike(EF.Functions.Unaccent(s.Customer.FullName), EF.Functions.Unaccent(patron))
                || EF.Functions.ILike(s.Customer.Document, patron));
        }

        if (filter.CustomerId is { } clienteId)
        {
            consulta = consulta.Where(s => s.CustomerId == clienteId);
        }

        return filter.Status.HasValue ? consulta.Where(s => s.Status == filter.Status.Value) : consulta;
    }
}
