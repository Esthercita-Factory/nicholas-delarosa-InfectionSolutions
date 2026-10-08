using InfectionSolutions.Application.Abstractions;
using InfectionSolutions.Application.Common;
using InfectionSolutions.Application.Dtos.Customers;
using InfectionSolutions.Domain.Entities;

namespace InfectionSolutions.Application.Services.Customers;

public sealed class CustomerService : ICustomerService
{
    private readonly ICustomerRepository _customers;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public CustomerService(ICustomerRepository customers, IUnitOfWork unitOfWork, IClock clock)
    {
        _customers = customers;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<PagedResponse<CustomerResponse>> ListAsync(CustomerQuery query, CancellationToken cancellationToken = default)
    {
        var page = new PageRequest { Page = query.Page, PageSize = query.PageSize };
        var termino = string.IsNullOrWhiteSpace(query.Q) ? null : query.Q.Trim();
        var result = await _customers.ListAsync(new CustomerFilter(termino, query.Active), page, cancellationToken);

        return PagedResponse<CustomerResponse>.From(result, page, ToResponse);
    }

    public async Task<IReadOnlyList<CustomerResponse>> ListAllAsync(CustomerQuery query, CancellationToken cancellationToken = default)
    {
        var termino = string.IsNullOrWhiteSpace(query.Q) ? null : query.Q.Trim();
        var customers = await _customers.ListAllAsync(new CustomerFilter(termino, query.Active), cancellationToken);
        return customers.Select(ToResponse).ToList();
    }

    public async Task<Result<CustomerResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var customer = await _customers.FindByIdAsync(id, cancellationToken);

        return customer is null
            ? Result.Failure<CustomerResponse>(Error.EntityNotFound("Cliente", id))
            : Result.Success(ToResponse(customer));
    }

    public async Task<IReadOnlyList<CustomerOption>> GetOptionsAsync(CancellationToken cancellationToken = default)
    {
        var customers = await _customers.GetActiveAsync(cancellationToken);
        return customers.Select(c => new CustomerOption(c.Id, c.Document, c.FullName)).ToList();
    }

    public async Task<Result<CustomerResponse>> CreateAsync(CustomerRequest request, CancellationToken cancellationToken = default)
    {
        var error = Validar(request) ?? await ValidarDuplicadosAsync(request, null, cancellationToken);
        if (error is not null)
        {
            return Result.Failure<CustomerResponse>(error);
        }

        var customer = new Customer { CreatedAt = _clock.UtcNow };
        AplicarCambios(request, customer);

        await _customers.AddAsync(customer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(ToResponse(customer));
    }

    public async Task<Result<CustomerResponse>> UpdateAsync(Guid id, CustomerRequest request, CancellationToken cancellationToken = default)
    {
        var customer = await _customers.FindByIdAsync(id, cancellationToken);
        if (customer is null)
        {
            return Result.Failure<CustomerResponse>(Error.EntityNotFound("Cliente", id));
        }

        var error = Validar(request) ?? await ValidarDuplicadosAsync(request, id, cancellationToken);
        if (error is not null)
        {
            return Result.Failure<CustomerResponse>(error);
        }

        AplicarCambios(request, customer);
        customer.UpdatedAt = _clock.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(ToResponse(customer));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var customer = await _customers.FindByIdAsync(id, cancellationToken);
        if (customer is null)
        {
            return Result.Failure(Error.EntityNotFound("Cliente", id));
        }

        if (await _customers.HasSalesAsync(id, cancellationToken))
        {
            return Result.Failure(Error.Conflict(
                "No se puede eliminar un cliente con ventas registradas. Desactívalo en su lugar."));
        }

        _customers.Remove(customer);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private static Error? Validar(CustomerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Document))
        {
            return Error.Validation("El documento es obligatorio.", nameof(request.Document));
        }

        if (!FieldRules.IsValidDocument(FieldRules.NormalizeDocument(request.Document)))
        {
            return Error.Validation(
                $"El documento debe tener entre {FieldRules.DocumentMinLength} y {FieldRules.DocumentMaxLength} letras o números.",
                nameof(request.Document));
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            return Error.Validation("El nombre es obligatorio.", nameof(request.FullName));
        }

        if (request.Age is < InputParser.MinAge or > InputParser.MaxAge)
        {
            return Error.Validation($"La edad debe estar entre {InputParser.MinAge} y {InputParser.MaxAge} años.", nameof(request.Age));
        }

        if (!FieldRules.IsValidEmail(request.Email))
        {
            return Error.Validation("El correo no tiene un formato válido.", nameof(request.Email));
        }

        if (!FieldRules.IsValidPhone(request.Phone))
        {
            return Error.Validation("El teléfono es obligatorio y solo admite números, espacios, guiones, paréntesis y + al inicio.", nameof(request.Phone));
        }

        return null;
    }

    /// <summary>
    /// document y email son unique en la base: se valida antes para devolver
    /// un mensaje en el campo en lugar de dejar estallar la excepcion.
    /// </summary>
    private async Task<Error?> ValidarDuplicadosAsync(CustomerRequest request, Guid? excluirId, CancellationToken cancellationToken)
    {
        if (await _customers.DocumentExistsAsync(FieldRules.NormalizeDocument(request.Document), excluirId, cancellationToken))
        {
            return Error.Conflict("Ya existe un cliente con ese documento.", nameof(request.Document));
        }

        if (await _customers.EmailExistsAsync(FieldRules.NormalizeEmail(request.Email), excluirId, cancellationToken))
        {
            return Error.Conflict("Ya existe un cliente con ese correo.", nameof(request.Email));
        }

        return null;
    }

    private static void AplicarCambios(CustomerRequest request, Customer customer)
    {
        customer.Document = FieldRules.NormalizeDocument(request.Document);
        customer.FullName = request.FullName.Trim();
        customer.Age = request.Age;
        customer.Email = FieldRules.NormalizeEmail(request.Email);
        customer.Phone = request.Phone.Trim();
        customer.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
        customer.IsActive = request.IsActive;
    }

    private static CustomerResponse ToResponse(Customer customer) => new(
        customer.Id,
        customer.Document,
        customer.FullName,
        customer.Age,
        customer.Email,
        customer.Phone,
        customer.Address,
        customer.IsActive,
        customer.UserId is not null,
        customer.CreatedAt,
        customer.UpdatedAt);
}
