using InfectionSolutions.Application.Common;
using InfectionSolutions.Application.Dtos.Auth;
using InfectionSolutions.Application.Services.Auth;
using InfectionSolutions.Application.Services.Notifications;
using InfectionSolutions.Domain.Identity;

namespace InfectionSolutions.Tests.Application;

public class AuthServiceTests
{
    private readonly IUserService _users = Substitute.For<IUserService>();
    private readonly ITokenService _tokens = Substitute.For<ITokenService>();
    private readonly ICustomerRepository _customers = Substitute.For<ICustomerRepository>();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FixedClock _clock = new(new DateTimeOffset(2026, 10, 8, 15, 0, 0, TimeSpan.Zero));
    private readonly ICustomerNotifier _notifier = Substitute.For<ICustomerNotifier>();
    private readonly AuthService _service;

    private readonly UserAccount _cuenta = new("user-1", "laura@correo.com", "Laura Mendoza");

    public AuthServiceTests()
    {
        _service = new AuthService(_users, _tokens, _customers, _unitOfWork, _clock, _notifier, TestMapper.Instance);

        _tokens.CreateToken(Arg.Any<UserAccount>(), Arg.Any<IReadOnlyCollection<string>>())
            .Returns(new AccessToken("jwt-de-prueba", _clock.UtcNow.AddHours(2)));
        _users.CreateAsync(Arg.Any<UserRegistration>(), ApplicationRoles.Customer, Arg.Any<CancellationToken>())
            .Returns(UserCreation.Success(_cuenta));
    }

    private static RegisterRequest Registro(string documento = "1.020.458.796", string correo = " Laura@Correo.com ") => new()
    {
        Document = documento,
        FullName = " Laura Mendoza ",
        Age = 34,
        Email = correo,
        Phone = "3105550101",
        Address = "Calle 100 # 15-20",
        Password = "Cliente123*",
        ConfirmPassword = "Cliente123*"
    };

    [Fact]
    public async Task Registro_CreaLaCuentaClienteYSuFicha_YEnviaLaBienvenida()
    {
        var resultado = await _service.RegisterCustomerAsync(Registro());

        Assert.True(resultado.IsSuccess);
        Assert.Equal("jwt-de-prueba", resultado.Value!.AccessToken);
        Assert.Equal([ApplicationRoles.Customer], resultado.Value.User.Roles);
        Assert.True(resultado.Value.User.Customer!.HasPortalAccess);

        await _users.Received(1).CreateAsync(
            Arg.Is<UserRegistration>(r => r.Email == "laura@correo.com" && r.FullName == "Laura Mendoza"),
            ApplicationRoles.Customer, Arg.Any<CancellationToken>());
        await _customers.Received(1).AddAsync(
            Arg.Is<Customer>(c => c.Document == "1020458796" && c.Email == "laura@correo.com" && c.UserId == "user-1"),
            Arg.Any<CancellationToken>());
        Assert.True(_unitOfWork.Committed);
        await _notifier.Received(1).SendWelcomeAsync("laura@correo.com", "Laura Mendoza", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Registro_ClienteQueYaExistiaSinCuenta_SeVinculaSinDuplicarlo()
    {
        var existente = new Customer { Id = Guid.NewGuid(), Document = "1020458796", Email = "laura@correo.com", FullName = "Laura M.", IsActive = true };
        _customers.FindByDocumentAsync("1020458796", Arg.Any<CancellationToken>()).Returns(existente);

        var resultado = await _service.RegisterCustomerAsync(Registro());

        Assert.True(resultado.IsSuccess);
        Assert.Equal("user-1", existente.UserId);
        Assert.Equal(existente.Id, resultado.Value!.User.Customer!.Id);
        await _customers.DidNotReceive().AddAsync(Arg.Any<Customer>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Registro_DocumentoQueYaTieneCuenta_EsConflicto()
    {
        _customers.FindByDocumentAsync("1020458796", Arg.Any<CancellationToken>())
            .Returns(new Customer { Document = "1020458796", Email = "laura@correo.com", UserId = "otro", IsActive = true });

        var resultado = await _service.RegisterCustomerAsync(Registro());

        Assert.Equal(ErrorCode.Conflict, resultado.Error!.Code);
        Assert.Equal(nameof(RegisterRequest.Document), resultado.Error.Field);
        await _users.DidNotReceiveWithAnyArgs().CreateAsync(default!, default!, default);
    }

    [Fact]
    public async Task Registro_ContrasenasDistintas_NoCreaNada()
    {
        var resultado = await _service.RegisterCustomerAsync(Registro() with { ConfirmPassword = "Otra123*" });

        Assert.Equal(ErrorCode.Validation, resultado.Error!.Code);
        Assert.Equal(nameof(RegisterRequest.ConfirmPassword), resultado.Error.Field);
        await _users.DidNotReceiveWithAnyArgs().CreateAsync(default!, default!, default);
    }

    [Fact]
    public async Task Registro_SiIdentityRechazaLaContrasena_DeshaceYNoCreaFicha()
    {
        _users.CreateAsync(Arg.Any<UserRegistration>(), ApplicationRoles.Customer, Arg.Any<CancellationToken>())
            .Returns(UserCreation.Failure([new UserError("Password", "La contraseña debe tener al menos una mayúscula.")]));

        var resultado = await _service.RegisterCustomerAsync(Registro());

        Assert.Equal(ErrorCode.Validation, resultado.Error!.Code);
        Assert.Equal("Password", resultado.Error.Field);
        Assert.True(_unitOfWork.RolledBack);
        Assert.False(_unitOfWork.Committed);
        await _customers.DidNotReceive().AddAsync(Arg.Any<Customer>(), Arg.Any<CancellationToken>());
        await _notifier.DidNotReceiveWithAnyArgs().SendWelcomeAsync(default!, default!, default);
    }

    [Fact]
    public async Task Login_CredencialesInvalidas_EsNoAutenticado()
    {
        _users.CheckPasswordAsync("laura@correo.com", "mala", Arg.Any<CancellationToken>())
            .Returns(new SignInAttempt(SignInOutcome.InvalidCredentials, null));

        var resultado = await _service.LoginAsync(new LoginRequest { Email = "Laura@correo.com", Password = "mala" });

        Assert.Equal(ErrorCode.Unauthorized, resultado.Error!.Code);
        _tokens.DidNotReceiveWithAnyArgs().CreateToken(default!, default!);
    }

    [Fact]
    public async Task Login_ClienteDesactivado_NoRecibeToken()
    {
        _users.CheckPasswordAsync("laura@correo.com", "Cliente123*", Arg.Any<CancellationToken>())
            .Returns(new SignInAttempt(SignInOutcome.Success, _cuenta));
        _users.GetRolesAsync("user-1", Arg.Any<CancellationToken>()).Returns([ApplicationRoles.Customer]);
        _customers.FindByUserIdAsync("user-1", Arg.Any<CancellationToken>()).Returns(new Customer { UserId = "user-1", IsActive = false });

        var resultado = await _service.LoginAsync(new LoginRequest { Email = "laura@correo.com", Password = "Cliente123*" });

        Assert.Equal(ErrorCode.Forbidden, resultado.Error!.Code);
    }

    [Fact]
    public async Task RecuperarContrasena_CorreoDeCliente_EnviaEnlaceConCorreoYToken()
    {
        _users.FindByEmailAsync("laura@correo.com", Arg.Any<CancellationToken>()).Returns(_cuenta);
        _users.GetRolesAsync("user-1", Arg.Any<CancellationToken>()).Returns([ApplicationRoles.Customer]);
        _users.CreatePasswordResetTokenAsync("user-1", Arg.Any<CancellationToken>()).Returns("tok_en-1");

        await _service.RequestPasswordResetAsync(" Laura@Correo.com ", "https://portal.test/restablecer", ApplicationRoles.Customer);

        await _notifier.Received(1).SendPasswordResetAsync(
            "laura@correo.com", "Laura Mendoza",
            "https://portal.test/restablecer?email=laura%40correo.com&token=tok_en-1",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecuperarContrasena_CorreoInexistente_NoEnviaNada()
    {
        await _service.RequestPasswordResetAsync("nadie@correo.com", "https://portal.test/restablecer", ApplicationRoles.Customer);

        await _users.DidNotReceiveWithAnyArgs().CreatePasswordResetTokenAsync(default!, default);
        await _notifier.DidNotReceiveWithAnyArgs().SendPasswordResetAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task RecuperarContrasena_DesdeElPanel_SoloParaAdministradores()
    {
        _users.FindByEmailAsync("laura@correo.com", Arg.Any<CancellationToken>()).Returns(_cuenta);
        _users.GetRolesAsync("user-1", Arg.Any<CancellationToken>()).Returns([ApplicationRoles.Customer]);

        await _service.RequestPasswordResetAsync("laura@correo.com", "https://panel.test/Account/ResetPassword", ApplicationRoles.Administrator);

        await _users.DidNotReceiveWithAnyArgs().CreatePasswordResetTokenAsync(default!, default);
        await _notifier.DidNotReceiveWithAnyArgs().SendPasswordResetAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task Restablecer_EnlaceVencido_EsErrorGeneralSinCampo()
    {
        _users.ResetPasswordAsync("laura@correo.com", "viejo", "Nueva123*", Arg.Any<CancellationToken>())
            .Returns([new UserError("Token", "El enlace no es válido o ya venció.")]);

        var resultado = await _service.ResetPasswordAsync(new ResetPasswordRequest
        {
            Email = "laura@correo.com", Token = "viejo", Password = "Nueva123*", ConfirmPassword = "Nueva123*"
        });

        Assert.Equal(ErrorCode.Validation, resultado.Error!.Code);
        Assert.Null(resultado.Error.Field);
    }

    [Fact]
    public async Task Restablecer_ContrasenaDebil_ErrorEnElCampo()
    {
        _users.ResetPasswordAsync("laura@correo.com", "tok", "debil", Arg.Any<CancellationToken>())
            .Returns([new UserError("Password", "La contraseña debe tener al menos 8 caracteres.")]);

        var resultado = await _service.ResetPasswordAsync(new ResetPasswordRequest
        {
            Email = "laura@correo.com", Token = "tok", Password = "debil", ConfirmPassword = "debil"
        });

        Assert.Equal("Password", resultado.Error!.Field);
    }

    [Fact]
    public async Task LoginExterno_CuentaNueva_SeCreaComoClienteSinFicha()
    {
        var google = new ExternalIdentity("Google", "g-123", "Laura@Gmail.com", "Laura Mendoza");
        var nueva = new UserAccount("user-g", "laura@gmail.com", "Laura Mendoza");
        _users.CreateExternalAsync(Arg.Any<ExternalIdentity>(), ApplicationRoles.Customer, Arg.Any<CancellationToken>())
            .Returns(UserCreation.Success(nueva));
        _users.GetRolesAsync("user-g", Arg.Any<CancellationToken>()).Returns([ApplicationRoles.Customer]);

        var resultado = await _service.ExternalLoginAsync(google);

        Assert.True(resultado.IsSuccess);
        Assert.Null(resultado.Value!.User.Customer);
        await _users.Received(1).CreateExternalAsync(
            Arg.Is<ExternalIdentity>(i => i.Email == "laura@gmail.com" && i.ProviderKey == "g-123"),
            ApplicationRoles.Customer, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginExterno_CorreoYaRegistrado_VinculaElProveedorSinCrearOtraCuenta()
    {
        var github = new ExternalIdentity("GitHub", "gh-9", "laura@correo.com", "");
        _users.FindByEmailAsync("laura@correo.com", Arg.Any<CancellationToken>()).Returns(_cuenta);
        _users.GetRolesAsync("user-1", Arg.Any<CancellationToken>()).Returns([ApplicationRoles.Customer]);
        _customers.FindByUserIdAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(new Customer { Id = Guid.NewGuid(), UserId = "user-1", Email = "laura@correo.com", IsActive = true });

        var resultado = await _service.ExternalLoginAsync(github);

        Assert.True(resultado.IsSuccess);
        Assert.NotNull(resultado.Value!.User.Customer);
        await _users.Received(1).AddExternalLoginAsync("user-1", Arg.Is<ExternalIdentity>(i => i.Provider == "GitHub"), Arg.Any<CancellationToken>());
        await _users.DidNotReceiveWithAnyArgs().CreateExternalAsync(default!, default!, default);
    }

    [Fact]
    public async Task LoginExterno_SinCorreo_NoCreaNada()
    {
        var resultado = await _service.ExternalLoginAsync(new ExternalIdentity("GitHub", "gh-1", "", "laura"));

        Assert.Equal(ErrorCode.Validation, resultado.Error!.Code);
        await _users.DidNotReceiveWithAnyArgs().CreateExternalAsync(default!, default!, default);
    }

    [Fact]
    public async Task CompletarDatos_CreaLaFichaConElCorreoDeLaCuentaYDaLaBienvenida()
    {
        _users.FindByIdAsync("user-1", Arg.Any<CancellationToken>()).Returns(_cuenta);
        _users.GetRolesAsync("user-1", Arg.Any<CancellationToken>()).Returns([ApplicationRoles.Customer]);

        var resultado = await _service.CompleteProfileAsync("user-1", new CompleteProfileRequest
        {
            Document = "1.020.458.796", FullName = "", Age = 34, Phone = "3105550101"
        });

        Assert.True(resultado.IsSuccess);
        await _customers.Received(1).AddAsync(
            Arg.Is<Customer>(c => c.Document == "1020458796" && c.Email == "laura@correo.com" && c.FullName == "Laura Mendoza" && c.UserId == "user-1"),
            Arg.Any<CancellationToken>());
        Assert.Equal(1, _unitOfWork.SaveCount);
        await _notifier.Received(1).SendWelcomeAsync("laura@correo.com", "Laura Mendoza", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompletarDatos_SiYaTieneFicha_EsConflicto()
    {
        _users.FindByIdAsync("user-1", Arg.Any<CancellationToken>()).Returns(_cuenta);
        _users.GetRolesAsync("user-1", Arg.Any<CancellationToken>()).Returns([ApplicationRoles.Customer]);
        _customers.FindByUserIdAsync("user-1", Arg.Any<CancellationToken>()).Returns(new Customer { UserId = "user-1" });

        var resultado = await _service.CompleteProfileAsync("user-1", new CompleteProfileRequest
        {
            Document = "1020458796", Age = 34, Phone = "3105550101"
        });

        Assert.Equal(ErrorCode.Conflict, resultado.Error!.Code);
        await _customers.DidNotReceive().AddAsync(Arg.Any<Customer>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Login_Correcto_DevuelveTokenConRoles()
    {
        _users.CheckPasswordAsync("admin@infection.com", "Admin123*", Arg.Any<CancellationToken>())
            .Returns(new SignInAttempt(SignInOutcome.Success, new UserAccount("admin-1", "admin@infection.com", "Admin")));
        _users.GetRolesAsync("admin-1", Arg.Any<CancellationToken>()).Returns([ApplicationRoles.Administrator]);

        var resultado = await _service.LoginAsync(new LoginRequest { Email = "admin@infection.com", Password = "Admin123*" });

        Assert.True(resultado.IsSuccess);
        Assert.Equal("Bearer", resultado.Value!.TokenType);
        Assert.Null(resultado.Value.User.Customer);
        _tokens.Received(1).CreateToken(
            Arg.Is<UserAccount>(u => u.Id == "admin-1"),
            Arg.Is<IReadOnlyCollection<string>>(r => r.Contains(ApplicationRoles.Administrator)));
    }
}
