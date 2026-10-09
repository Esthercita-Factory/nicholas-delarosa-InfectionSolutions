namespace InfectionSolutions.Application.Abstractions;

/// <summary>Cuenta de Identity vista desde Application (sin depender de ASP.NET Identity).</summary>
public sealed record UserAccount(string Id, string Email, string FullName);

public sealed record UserRegistration(string Email, string FullName, string Password);

/// <summary>Error de Identity con el campo al que corresponde (Email o Password).</summary>
public sealed record UserError(string Field, string Message);

public sealed record UserCreation(UserAccount? User, IReadOnlyList<UserError> Errors)
{
    public bool Succeeded => User is not null;

    public static UserCreation Success(UserAccount user) => new(user, []);

    public static UserCreation Failure(IReadOnlyList<UserError> errors) => new(null, errors);
}

public enum SignInOutcome
{
    Success,
    InvalidCredentials,
    LockedOut
}

public sealed record SignInAttempt(SignInOutcome Outcome, UserAccount? User);

public interface IUserService
{
    Task<UserAccount?> FindByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Crea la cuenta con el rol dado. Si el rol no se puede asignar, la cuenta se borra.</summary>
    Task<UserCreation> CreateAsync(UserRegistration registration, string role, CancellationToken cancellationToken = default);

    /// <summary>Verifica la contraseña con bloqueo por intentos fallidos (la misma politica del panel).</summary>
    Task<SignInAttempt> CheckPasswordAsync(string email, string password, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetRolesAsync(string userId, CancellationToken cancellationToken = default);
}
