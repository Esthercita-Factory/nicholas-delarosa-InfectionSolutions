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
}
