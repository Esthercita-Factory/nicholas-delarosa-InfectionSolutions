namespace InfectionSolutions.Application.Dtos.Customers;

public sealed record CustomerRequest
{
    public string Document { get; init; } = string.Empty;

    public string FullName { get; init; } = string.Empty;

    public int Age { get; init; }

    public string Email { get; init; } = string.Empty;

    public string Phone { get; init; } = string.Empty;

    public string? Address { get; init; }

    public bool IsActive { get; init; } = true;
}

public sealed record CustomerResponse(
    Guid Id,
    string Document,
    string FullName,
    int Age,
    string Email,
    string Phone,
    string? Address,
    bool IsActive,
    bool HasPortalAccess,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record CustomerQuery
{
    /// <summary>Busca por nombre o documento.</summary>
    public string? Q { get; init; }

    public bool OnlyActive { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 10;
}

/// <summary>Opcion para el selector de clientes al registrar una venta.</summary>
public sealed record CustomerOption(Guid Id, string Document, string FullName);
