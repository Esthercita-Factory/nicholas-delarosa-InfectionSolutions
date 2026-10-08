namespace InfectionSolutions.Application.Dtos.Products;

public sealed record ProductRequest
{
    public string Sku { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string Category { get; init; } = string.Empty;

    public string Unit { get; init; } = "Unidad";

    public decimal Price { get; init; }

    public int Stock { get; init; }

    public bool IsActive { get; init; } = true;
}

public sealed record ProductResponse(
    Guid Id,
    string Sku,
    string Name,
    string? Description,
    string Category,
    string Unit,
    decimal Price,
    int Stock,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record ProductQuery
{
    /// <summary>Busca por nombre, codigo o categoria.</summary>
    public string? Q { get; init; }

    public string? Category { get; init; }

    public bool OnlyActive { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 10;
}

/// <summary>Opcion para el selector de productos al registrar una venta.</summary>
public sealed record ProductOption(Guid Id, string Sku, string Name, string Unit, decimal Price, int Stock);
