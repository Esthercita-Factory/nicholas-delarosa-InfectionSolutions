using InfectionSolutions.Application.Abstractions;
using Microsoft.AspNetCore.Identity;

namespace InfectionSolutions.Infrastructure.Identity;

public sealed class IdentityUserService : IUserService
{
    // Hash de relleno: si el correo no existe se verifica igual una contraseña,
    // para que el tiempo de respuesta no delate que cuentas existen.
    private static readonly Lazy<string> HashDeRelleno = new(() =>
        new PasswordHasher<ApplicationUser>().HashPassword(new ApplicationUser(), Guid.NewGuid().ToString("N")));

    private readonly UserManager<ApplicationUser> _userManager;

    public IdentityUserService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<UserAccount?> FindByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(id);
        return user is null ? null : ACuenta(user);
    }

    public async Task<UserCreation> CreateAsync(UserRegistration registration, string role, CancellationToken cancellationToken = default)
    {
        var user = new ApplicationUser
        {
            UserName = registration.Email,
            Email = registration.Email,
            EmailConfirmed = true,
            FullName = registration.FullName
        };

        var creacion = await _userManager.CreateAsync(user, registration.Password);
        if (!creacion.Succeeded)
        {
            return UserCreation.Failure(creacion.Errors.Select(AError).ToList());
        }

        var rol = await _userManager.AddToRoleAsync(user, role);
        if (!rol.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            return UserCreation.Failure(rol.Errors.Select(AError).ToList());
        }

        return UserCreation.Success(ACuenta(user));
    }

    public async Task<SignInAttempt> CheckPasswordAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            new PasswordHasher<ApplicationUser>().VerifyHashedPassword(new ApplicationUser(), HashDeRelleno.Value, password);
            return new SignInAttempt(SignInOutcome.InvalidCredentials, null);
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            return new SignInAttempt(SignInOutcome.LockedOut, null);
        }

        if (!await _userManager.CheckPasswordAsync(user, password))
        {
            await _userManager.AccessFailedAsync(user);
            return await _userManager.IsLockedOutAsync(user)
                ? new SignInAttempt(SignInOutcome.LockedOut, null)
                : new SignInAttempt(SignInOutcome.InvalidCredentials, null);
        }

        await _userManager.ResetAccessFailedCountAsync(user);
        return new SignInAttempt(SignInOutcome.Success, ACuenta(user));
    }

    public async Task<IReadOnlyList<string>> GetRolesAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        return user is null ? [] : [.. await _userManager.GetRolesAsync(user)];
    }

    private static UserAccount ACuenta(ApplicationUser user)
        => new(user.Id, user.Email ?? user.UserName ?? string.Empty, user.FullName);

    /// <summary>Los codigos de Identity dicen a que campo pertenece cada error.</summary>
    private static UserError AError(IdentityError error)
        => new(error.Code.Contains("Email") || error.Code.Contains("UserName") ? "Email" : "Password", error.Description);
}
