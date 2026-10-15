using System.Security.Claims;
using AutoMapper;
using InfectionSolutions.Api.Common;
using InfectionSolutions.Api.ViewModels.Auth;
using InfectionSolutions.Application.Abstractions;
using InfectionSolutions.Application.Common;
using InfectionSolutions.Application.Dtos.Auth;
using InfectionSolutions.Application.Services.Auth;
using InfectionSolutions.Domain.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace InfectionSolutions.Api.Controllers;

/// <summary>Autenticación con JWT para administradores y clientes.</summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly IMapper _mapper;
    private readonly IAuthenticationSchemeProvider _schemes;
    private readonly ExternalLoginCodes _codes;
    private readonly PortalOptions _portal;

    public AuthController(
        IAuthService auth,
        IMapper mapper,
        IAuthenticationSchemeProvider schemes,
        ExternalLoginCodes codes,
        IOptions<PortalOptions> portal)
    {
        _auth = auth;
        _mapper = mapper;
        _schemes = schemes;
        _codes = codes;
        _portal = portal.Value;
    }

    /// <summary>Inicia sesión y devuelve el token (envíalo como "Authorization: Bearer {token}").</summary>
    /// <response code="200">Credenciales válidas.</response>
    /// <response code="401">Correo o contraseña incorrectos, o cuenta bloqueada por intentos fallidos.</response>
    /// <response code="403">Cliente desactivado por la empresa.</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AuthResponse>> Login(LoginViewModel model, CancellationToken cancellationToken)
        => this.OkOrProblem(await _auth.LoginAsync(_mapper.Map<LoginRequest>(model), cancellationToken));

    /// <summary>
    /// Registro público de clientes: crea la cuenta con rol Cliente y su ficha de cliente,
    /// envía el correo de bienvenida y devuelve el token (la sesión queda iniciada).
    /// </summary>
    /// <remarks>
    /// Si la empresa ya tenía al cliente registrado con el mismo documento y correo,
    /// la cuenta se vincula a esa ficha y conserva su historial de compras.
    /// </remarks>
    /// <response code="201">Cuenta creada.</response>
    /// <response code="400">Datos inválidos o contraseña que no cumple la política.</response>
    /// <response code="409">Documento o correo ya registrados.</response>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponse>> Register(RegisterViewModel model, CancellationToken cancellationToken)
    {
        var resultado = await _auth.RegisterCustomerAsync(_mapper.Map<RegisterRequest>(model), cancellationToken);

        return resultado.IsSuccess
            ? CreatedAtAction(nameof(Me), null, resultado.Value)
            : this.ProblemFor(resultado.Error!);
    }

    /// <summary>Perfil del usuario autenticado, con su ficha de cliente si la tiene.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserProfile), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserProfile>> Me(CancellationToken cancellationToken)
        => this.OkOrProblem(await _auth.GetProfileAsync(User.UserId(), cancellationToken));

    /// <summary>"Olvidé mi contraseña" de un cliente: envía al correo un enlace al portal para elegir una nueva.</summary>
    /// <remarks>Responde igual exista o no la cuenta, para no revelar qué correos están registrados.</remarks>
    /// <response code="204">Si el correo es de un cliente, el enlace va en camino (vence en una hora).</response>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ForgotPassword(ForgotPasswordViewModel model, CancellationToken cancellationToken)
    {
        await _auth.RequestPasswordResetAsync(model.Email, _portal.Url("/restablecer"), ApplicationRoles.Customer, cancellationToken);
        return NoContent();
    }

    /// <summary>Cambia la contraseña con el token del correo y devuelve la sesión iniciada.</summary>
    /// <response code="200">Contraseña cambiada; trae el token.</response>
    /// <response code="400">Enlace vencido o ya usado, o contraseña que no cumple la política.</response>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthResponse>> ResetPassword(ResetPasswordViewModel model, CancellationToken cancellationToken)
    {
        var cambio = await _auth.ResetPasswordAsync(_mapper.Map<ResetPasswordRequest>(model), cancellationToken);
        if (cambio.IsFailure)
        {
            return this.ProblemFor(cambio.Error!);
        }

        return this.OkOrProblem(await _auth.LoginAsync(new LoginRequest { Email = model.Email, Password = model.Password }, cancellationToken));
    }

    /// <summary>Proveedores externos configurados ("Google", "GitHub"): el portal solo muestra esos botones.</summary>
    [HttpGet("external/providers")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<string>>> ExternalProviders()
        => Ok(await ProveedoresActivosAsync());

    /// <summary>
    /// Empieza el inicio de sesión con Google o GitHub. Se abre con el navegador (no con fetch):
    /// redirige al proveedor y, al volver, al portal en /ingresar/externo.
    /// </summary>
    /// <param name="provider">Google o GitHub.</param>
    /// <param name="returnUrl">Ruta del portal a la que volver después (por ejemplo /carrito).</param>
    [HttpGet("external/{provider}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExternalChallenge(string provider, string? returnUrl)
    {
        var esquema = (await ProveedoresActivosAsync()).FirstOrDefault(p => string.Equals(p, provider, StringComparison.OrdinalIgnoreCase));
        if (esquema is null)
        {
            return this.ProblemFor(new Error(ErrorCode.NotFound, $"El inicio de sesión con {provider} no está disponible."));
        }

        var propiedades = new AuthenticationProperties
        {
            RedirectUri = Url.Action(nameof(ExternalCallback), new { returnUrl = RutaLocal(returnUrl) })
        };
        propiedades.Items[ExternalLogin.ProviderItem] = esquema;

        return Challenge(propiedades, esquema);
    }

    /// <summary>Vuelta del proveedor: crea o vincula la cuenta y manda al portal un código de un solo uso.</summary>
    [HttpGet("external/callback")]
    [AllowAnonymous]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> ExternalCallback(string? returnUrl, CancellationToken cancellationToken)
    {
        var autenticacion = await HttpContext.AuthenticateAsync(ExternalLogin.CookieScheme);
        await HttpContext.SignOutAsync(ExternalLogin.CookieScheme);

        string? proveedor = null;
        autenticacion.Properties?.Items.TryGetValue(ExternalLogin.ProviderItem, out proveedor);
        var principal = autenticacion.Principal;
        var clave = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!autenticacion.Succeeded || principal is null || proveedor is null || clave is null)
        {
            return VolverAlLoginConError("No se pudo completar el inicio de sesión. Intenta de nuevo.");
        }

        var identidad = new ExternalIdentity(
            proveedor,
            clave,
            principal.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            // GitHub manda el usuario en Name y el nombre real en urn:github:name.
            principal.FindFirstValue("urn:github:name") ?? principal.FindFirstValue(ClaimTypes.Name) ?? string.Empty);

        var resultado = await _auth.ExternalLoginAsync(identidad, cancellationToken);
        if (resultado.IsFailure)
        {
            return VolverAlLoginConError(resultado.Error!.Message);
        }

        var codigo = _codes.Store(resultado.Value!);
        return Redirect(_portal.Url($"/ingresar/externo#codigo={codigo}&volver={Uri.EscapeDataString(RutaLocal(returnUrl))}"));
    }

    /// <summary>Cambia el código que dejó Google o GitHub por la sesión (sirve una sola vez y vence en dos minutos).</summary>
    /// <response code="200">Sesión iniciada. Si "user.customer" es null, el cliente debe completar sus datos.</response>
    /// <response code="401">Código inválido, usado o vencido.</response>
    [HttpPost("external/exchange")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public ActionResult<AuthResponse> ExternalExchange(ExternalCodeViewModel model)
    {
        var sesion = _codes.Take(model.Code);
        return sesion is null
            ? this.ProblemFor(Error.Unauthorized("El inicio de sesión ya no es válido. Vuelve a intentarlo."))
            : Ok(sesion);
    }

    /// <summary>
    /// Completa la ficha de cliente de una cuenta creada con Google o GitHub (documento, edad,
    /// teléfono…). Devuelve la sesión actualizada, ya con la ficha.
    /// </summary>
    /// <response code="200">Ficha creada (o vinculada a la que la empresa ya tenía).</response>
    /// <response code="409">El documento ya tiene cuenta, o los datos ya estaban completos.</response>
    [HttpPost("complete-profile")]
    [Authorize(Policy = AuthorizationPolicies.CustomerOnly)]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponse>> CompleteProfile(CompleteProfileViewModel model, CancellationToken cancellationToken)
        => this.OkOrProblem(await _auth.CompleteProfileAsync(User.UserId(), _mapper.Map<CompleteProfileRequest>(model), cancellationToken));

    private async Task<IReadOnlyList<string>> ProveedoresActivosAsync()
    {
        var registrados = (await _schemes.GetAllSchemesAsync()).Select(s => s.Name).ToHashSet();
        return ExternalLogin.Providers.Where(registrados.Contains).ToList();
    }

    private RedirectResult VolverAlLoginConError(string mensaje)
        => Redirect(_portal.Url($"/ingresar?externo={Uri.EscapeDataString(mensaje)}"));

    /// <summary>Solo rutas internas del portal: un returnUrl=https://... no saca al cliente del sitio.</summary>
    private static string RutaLocal(string? ruta)
        => ruta is { Length: > 0 } && ruta.StartsWith('/') && !ruta.StartsWith("//") && !ruta.StartsWith("/\\") ? ruta : "/catalogo";
}
