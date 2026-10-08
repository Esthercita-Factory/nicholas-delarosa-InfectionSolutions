using InfectionSolutions.Application.Abstractions;
using InfectionSolutions.Application.Common;
using InfectionSolutions.Domain.Entities;
using InfectionSolutions.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InfectionSolutions.Infrastructure.Repositories;

public sealed class CustomerRepository : ICustomerRepository
{
    private readonly ApplicationDbContext _db;

    public CustomerRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<Customer>> ListAsync(CustomerFilter filter, PageRequest page, CancellationToken cancellationToken = default)
    {
        var consulta = _db.Customers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Term))
        {
            var patron = $"%{filter.Term}%";
            consulta = consulta.Where(c =>
                EF.Functions.ILike(c.FullName, patron)
                || EF.Functions.ILike(c.Document, patron));
        }

        if (filter.OnlyActive)
        {
            consulta = consulta.Where(c => c.IsActive);
        }

        var total = await consulta.CountAsync(cancellationToken);
        var items = await consulta
            .OrderByDescending(c => c.IsActive)
            .ThenBy(c => c.FullName)
            .Skip(page.Skip)
            .Take(page.Take)
            .ToListAsync(cancellationToken);

        return new PagedResult<Customer> { Items = items, TotalCount = total };
    }

    public Task<Customer?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _db.Customers.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Customer>> GetActiveAsync(CancellationToken cancellationToken = default)
        => await _db.Customers
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.FullName)
            .ToListAsync(cancellationToken);

    public Task<bool> DocumentExistsAsync(string document, Guid? excludedId = null, CancellationToken cancellationToken = default)
        => _db.Customers.AnyAsync(c => c.Document == document && (excludedId == null || c.Id != excludedId), cancellationToken);

    public Task<bool> EmailExistsAsync(string email, Guid? excludedId = null, CancellationToken cancellationToken = default)
        => _db.Customers.AnyAsync(c => c.Email == email && (excludedId == null || c.Id != excludedId), cancellationToken);

    public Task<bool> HasSalesAsync(Guid id, CancellationToken cancellationToken = default)
        => _db.Sales.AnyAsync(s => s.CustomerId == id, cancellationToken);

    public async Task AddAsync(Customer customer, CancellationToken cancellationToken = default)
        => await _db.Customers.AddAsync(customer, cancellationToken);

    public void Remove(Customer customer) => _db.Customers.Remove(customer);
}
