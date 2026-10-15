using System.Text;
using InfectionSolutions.Application.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace InfectionSolutions.Infrastructure.Identity;

public sealed class IdentityUserService : IUserService
{
    // Hash de relleno: si el correo no existe se verifica igual una contraseña,
    // para que el tiempo de respuesta no delate que cuentas existen.
    private static readonly Lazy<string> HashDeRelleno = new(() =>
        new PasswordHasher<ApplicationUser>().HashPassword(new ApplicationUser(), Guid.NewGuid().ToString("N")));

    private static readonly UserError TokenInvalido = new("Token", "El enlace no es válido o ya venció.");

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

    public async Task<UserAccount?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
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

        return await AsignarRolAsync(user, role);
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

    public async Task<string?> CreatePasswordResetTokenAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return null;
        }

        // El token de Identity trae + / =: en Base64Url viaja limpio en el enlace del correo.
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        return WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
    }

    public async Task<IReadOnlyList<UserError>> ResetPasswordAsync(string email, string token, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return [TokenInvalido];
        }

        string original;
        try
        {
            original = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
        }
        catch (FormatException)
        {
            return [TokenInvalido];
        }

        var cambio = await _userManager.ResetPasswordAsync(user, original, newPassword);
        if (!cambio.Succeeded)
        {
            return cambio.Errors.Select(AError).ToList();
        }

        // Quien acaba de demostrar que es dueño del correo no debe seguir bloqueado.
        await _userManager.SetLockoutEndDateAsync(user, null);
        await _userManager.ResetAccessFailedCountAsync(user);
        return [];
    }

    public async Task<UserAccount?> FindByExternalLoginAsync(string provider, string providerKey, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByLoginAsync(provider, providerKey);
        return user is null ? null : ACuenta(user);
    }

    public async Task<UserCreation> CreateExternalAsync(ExternalIdentity identity, string role, CancellationToken cancellationToken = default)
    {
        var user = new ApplicationUser
        {
            UserName = identity.Email,
            Email = identity.Email,
            // El proveedor ya verifico el correo.
            EmailConfirmed = true,
            FullName = identity.FullName
        };

        // Sin contraseña: entra con el proveedor (o crea una con "olvide mi contraseña").
        var creacion = await _userManager.CreateAsync(user);
        if (!creacion.Succeeded)
        {
            return UserCreation.Failure(creacion.Errors.Select(AError).ToList());
        }

        var vinculo = await _userManager.AddLoginAsync(user, new UserLoginInfo(identity.Provider, identity.ProviderKey, identity.Provider));
        if (!vinculo.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            return UserCreation.Failure(vinculo.Errors.Select(AError).ToList());
        }

        return await AsignarRolAsync(user, role);
    }

    public async Task AddExternalLoginAsync(string userId, ExternalIdentity identity, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return;
        }

        var vinculados = await _userManager.GetLoginsAsync(user);
        if (!vinculados.Any(l => l.LoginProvider == identity.Provider && l.ProviderKey == identity.ProviderKey))
        {
            await _userManager.AddLoginAsync(user, new UserLoginInfo(identity.Provider, identity.ProviderKey, identity.Provider));
        }
    }

    /// <summary>Si el rol no se puede asignar, la cuenta se borra: no quedan cuentas sin rol.</summary>
    private async Task<UserCreation> AsignarRolAsync(ApplicationUser user, string role)
    {
        var rol = await _userManager.AddToRoleAsync(user, role);
        if (!rol.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            return UserCreation.Failure(rol.Errors.Select(AError).ToList());
        }

        return UserCreation.Success(ACuenta(user));
    }

    private static UserAccount ACuenta(ApplicationUser user)
        => new(user.Id, user.Email ?? user.UserName ?? string.Empty, user.FullName);

    /// <summary>Los codigos de Identity dicen a que campo pertenece cada error.</summary>
    private static UserError AError(IdentityError error)
    {
        var campo = error.Code switch
        {
            nameof(IdentityErrorDescriber.InvalidToken) => "Token",
            _ when error.Code.Contains("Email") || error.Code.Contains("UserName") => "Email",
            _ => "Password"
        };

        return new UserError(campo, error.Description);
    }
}
