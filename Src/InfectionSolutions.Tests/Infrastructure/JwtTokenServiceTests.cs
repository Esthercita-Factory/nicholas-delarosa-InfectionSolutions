using InfectionSolutions.Domain.Identity;
using InfectionSolutions.Infrastructure.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace InfectionSolutions.Tests.Infrastructure;

public class JwtTokenServiceTests
{
    private static readonly JwtOptions Opciones = new()
    {
        Issuer = "InfectionSolutions.Api",
        Audience = "InfectionSolutions.Clients",
        SigningKey = "clave-de-pruebas-con-mas-de-32-caracteres",
        AccessTokenMinutes = 60
    };

    private static TokenValidationParameters Validacion(string clave) => new()
    {
        ValidIssuer = Opciones.Issuer,
        ValidAudience = Opciones.Audience,
        IssuerSigningKey = JwtTokenService.SigningKey(new JwtOptions { SigningKey = clave }),
        // El token se emite con un reloj fijo: aqui solo se valida firma, emisor y audiencia.
        ValidateLifetime = false
    };

    private static AccessToken Emitir()
    {
        var reloj = new FixedClock(new DateTimeOffset(2026, 10, 8, 15, 0, 0, TimeSpan.Zero));
        var servicio = new JwtTokenService(Options.Create(Opciones), reloj);
        return servicio.CreateToken(new UserAccount("user-1", "laura@correo.com", "Laura Mendoza"), [ApplicationRoles.Customer]);
    }

    [Fact]
    public async Task ElToken_LlevaUsuarioCorreoRolYVencimiento()
    {
        var token = Emitir();

        var resultado = await new JsonWebTokenHandler().ValidateTokenAsync(token.Token, Validacion(Opciones.SigningKey));

        Assert.True(resultado.IsValid);
        Assert.Equal("user-1", resultado.Claims[JwtTokenService.SubjectClaim]);
        Assert.Equal("laura@correo.com", resultado.Claims[JwtTokenService.EmailClaim]);
        Assert.Equal(ApplicationRoles.Customer, resultado.Claims[JwtTokenService.RoleClaim]);
        Assert.Equal(new DateTimeOffset(2026, 10, 8, 16, 0, 0, TimeSpan.Zero), token.ExpiresAt);
    }

    [Fact]
    public async Task ElToken_FirmadoConOtraClave_NoEsValido()
    {
        var token = Emitir();

        var resultado = await new JsonWebTokenHandler().ValidateTokenAsync(token.Token, Validacion("otra-clave-distinta-de-mas-de-32-caracteres"));

        Assert.False(resultado.IsValid);
    }
}
