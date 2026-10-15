using InfectionSolutions.Application.Abstractions;
using InfectionSolutions.Application.Common;
using InfectionSolutions.Application.Dtos.Auth;

namespace InfectionSolutions.Application.Services.Auth;

public interface IAuthService
{
    /// <summary>Valida las credenciales y emite el token. Sirve para administradores y clientes.</summary>
    Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registro publico: crea la cuenta con rol Cliente y su ficha de cliente (o
    /// vincula la que el administrador ya habia creado con el mismo documento y
    /// correo), envia el correo de bienvenida y deja la sesion iniciada.
    /// </summary>
    Task<Result<AuthResponse>> RegisterCustomerAsync(RegisterRequest request, CancellationToken cancellationToken = default);

    Task<Result<UserProfile>> GetProfileAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// "Olvide mi contraseña": si existe una cuenta con ese correo y el rol pedido, le envia
    /// un enlace a resetPageUrl con el correo y el token. No dice si la cuenta existe.
    /// </summary>
    /// <param name="resetPageUrl">Pagina donde se escribe la nueva contraseña (portal o panel).</param>
    /// <param name="requiredRole">Solo se envia a cuentas con este rol: el portal es de clientes y el panel de administradores.</param>
    Task RequestPasswordResetAsync(string email, string resetPageUrl, string requiredRole, CancellationToken cancellationToken = default);

    Task<Result> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inicio de sesion con Google o GitHub (rol Cliente). Busca la cuenta por el proveedor o
    /// por el correo (y la vincula); si no existe la crea sin ficha de cliente: el portal pide
    /// completar los datos con CompleteProfileAsync antes de comprar.
    /// </summary>
    Task<Result<AuthResponse>> ExternalLoginAsync(ExternalIdentity identity, CancellationToken cancellationToken = default);

    /// <summary>Crea (o vincula) la ficha de cliente de una cuenta que aun no la tiene.</summary>
    Task<Result<AuthResponse>> CompleteProfileAsync(string userId, CompleteProfileRequest request, CancellationToken cancellationToken = default);
}
