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
        var consulta = Filtrar(_db.Customers.AsNoTracking(), filter);

        var total = await consulta.CountAsync(cancellationToken);
        var items = await Ordenar(consulta)
            .Skip(page.Skip)
            .Take(page.Take)
            .ToListAsync(cancellationToken);

        return new PagedResult<Customer> { Items = items, TotalCount = total };
    }

    public async Task<IReadOnlyList<Customer>> ListAllAsync(CustomerFilter filter, CancellationToken cancellationToken = default)
        => await Ordenar(Filtrar(_db.Customers.AsNoTracking(), filter)).ToListAsync(cancellationToken);

    public Task<Customer?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _db.Customers.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<Customer?> FindByUserIdAsync(string userId, CancellationToken cancellationToken = default)
        => _db.Customers.FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

    public Task<Customer?> FindByDocumentAsync(string document, CancellationToken cancellationToken = default)
        => _db.Customers.FirstOrDefaultAsync(c => c.Document == document, cancellationToken);

    public async Task<IReadOnlyList<Customer>> FindByDocumentsAsync(IReadOnlyCollection<string> documents, CancellationToken cancellationToken = default)
    {
        if (documents.Count == 0)
        {
            return [];
        }

        return await _db.Customers.Where(c => documents.Contains(c.Document)).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Customer>> FindByEmailsAsync(IReadOnlyCollection<string> emails, CancellationToken cancellationToken = default)
    {
        if (emails.Count == 0)
        {
            return [];
        }

        return await _db.Customers.Where(c => emails.Contains(c.Email)).ToListAsync(cancellationToken);
    }

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

    private static IQueryable<Customer> Filtrar(IQueryable<Customer> consulta, CustomerFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.Term))
        {
            var patron = SearchPatterns.Contains(filter.Term);

            // "1.020.458" encuentra el documento 1020458796: se compara tambien sin separadores.
            var soloDocumento = FieldRules.NormalizeDocument(filter.Term);
            var documento = soloDocumento.Length == 0 ? patron : SearchPatterns.Contains(soloDocumento);
            consulta = consulta.Where(c =>
                EF.Functions.ILike(EF.Functions.Unaccent(c.FullName), EF.Functions.Unaccent(patron))
                || EF.Functions.ILike(c.Document, patron)
                || EF.Functions.ILike(c.Document, documento)
                || EF.Functions.ILike(c.Email, patron));
        }

        return filter.Active is { } activo ? consulta.Where(c => c.IsActive == activo) : consulta;
    }

    private static IQueryable<Customer> Ordenar(IQueryable<Customer> consulta)
        => consulta.OrderByDescending(c => c.IsActive).ThenBy(c => c.FullName);
}
