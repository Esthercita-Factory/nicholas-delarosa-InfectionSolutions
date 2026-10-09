namespace InfectionSolutions.Application.Abstractions;

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

/// <summary>Emite los tokens de acceso de la API (JWT en Infrastructure).</summary>
public interface ITokenService
{
    AccessToken CreateToken(UserAccount user, IReadOnlyCollection<string> roles);
}
