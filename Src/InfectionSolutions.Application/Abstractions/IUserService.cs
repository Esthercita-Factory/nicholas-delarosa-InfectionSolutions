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

/// <summary>Cuenta de un proveedor externo (Google, GitHub) tal como llega despues de autorizar.</summary>
/// <param name="Provider">Nombre del esquema: "Google" o "GitHub".</param>
/// <param name="ProviderKey">Id del usuario en ese proveedor (no cambia aunque cambie el correo).</param>
public sealed record ExternalIdentity(string Provider, string ProviderKey, string Email, string FullName);

public interface IUserService
{
    Task<UserAccount?> FindByIdAsync(string id, CancellationToken cancellationToken = default);

    Task<UserAccount?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Crea la cuenta con el rol dado. Si el rol no se puede asignar, la cuenta se borra.</summary>
    Task<UserCreation> CreateAsync(UserRegistration registration, string role, CancellationToken cancellationToken = default);

    /// <summary>Verifica la contraseña con bloqueo por intentos fallidos (la misma politica del panel).</summary>
    Task<SignInAttempt> CheckPasswordAsync(string email, string password, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetRolesAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Token de un solo uso para cambiar la contraseña, ya seguro para ir en una URL. null si la cuenta no existe.</summary>
    Task<string?> CreatePasswordResetTokenAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cambia la contraseña con el token del correo y quita un bloqueo por intentos fallidos.
    /// Lista vacia = exito. Un correo inexistente responde como token invalido (no revela cuentas).
    /// </summary>
    Task<IReadOnlyList<UserError>> ResetPasswordAsync(string email, string token, string newPassword, CancellationToken cancellationToken = default);

    Task<UserAccount?> FindByExternalLoginAsync(string provider, string providerKey, CancellationToken cancellationToken = default);

    /// <summary>Crea la cuenta sin contraseña, vinculada al proveedor y con el rol dado.</summary>
    Task<UserCreation> CreateExternalAsync(ExternalIdentity identity, string role, CancellationToken cancellationToken = default);

    /// <summary>Vincula el proveedor a una cuenta existente (no hace nada si ya estaba vinculado).</summary>
    Task AddExternalLoginAsync(string userId, ExternalIdentity identity, CancellationToken cancellationToken = default);
}
