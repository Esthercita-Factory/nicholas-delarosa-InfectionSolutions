using AutoMapper;
using InfectionSolutions.Api.Common;
using InfectionSolutions.Api.ViewModels.Auth;
using InfectionSolutions.Application.Dtos.Auth;
using InfectionSolutions.Application.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfectionSolutions.Api.Controllers;

/// <summary>Autenticación con JWT para administradores y clientes.</summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly IMapper _mapper;

    public AuthController(IAuthService auth, IMapper mapper)
    {
        _auth = auth;
        _mapper = mapper;
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
}
