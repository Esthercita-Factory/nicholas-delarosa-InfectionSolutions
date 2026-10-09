using InfectionSolutions.Application.Dtos.Customers;

namespace InfectionSolutions.Application.Dtos.Auth;

public sealed record LoginRequest
{
    public string Email { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;
}

/// <summary>Registro publico del portal: crea la cuenta (rol Cliente) y su ficha de cliente.</summary>
public sealed record RegisterRequest
{
    public string Document { get; init; } = string.Empty;

    public string FullName { get; init; } = string.Empty;

    public int Age { get; init; }

    public string Email { get; init; } = string.Empty;

    public string Phone { get; init; } = string.Empty;

    public string? Address { get; init; }

    public string Password { get; init; } = string.Empty;

    public string ConfirmPassword { get; init; } = string.Empty;
}

/// <param name="Customer">La ficha de cliente vinculada; null para administradores.</param>
public sealed record UserProfile(
    string Id,
    string Email,
    string FullName,
    IReadOnlyList<string> Roles,
    CustomerResponse? Customer);

public sealed record AuthResponse(
    string TokenType,
    string AccessToken,
    DateTimeOffset ExpiresAt,
    UserProfile User);
