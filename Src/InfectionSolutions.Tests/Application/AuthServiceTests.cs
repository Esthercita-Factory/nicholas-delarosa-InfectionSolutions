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
