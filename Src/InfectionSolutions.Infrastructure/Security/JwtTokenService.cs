using System.Security.Claims;
using System.Text;
using InfectionSolutions.Application.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace InfectionSolutions.Infrastructure.Security;

public sealed class JwtTokenService : ITokenService
{
    // Nombres cortos de los claims: el SPA los lee tal cual al decodificar el token.
    public const string SubjectClaim = JwtRegisteredClaimNames.Sub;
    public const string EmailClaim = JwtRegisteredClaimNames.Email;
    public const string NameClaim = "name";
    public const string RoleClaim = "role";

    private readonly JwtOptions _options;
    private readonly IClock _clock;

    public JwtTokenService(IOptions<JwtOptions> options, IClock clock)
    {
        _options = options.Value;
        _clock = clock;
    }

    public static SymmetricSecurityKey SigningKey(JwtOptions options) => new(Encoding.UTF8.GetBytes(options.SigningKey));

    public AccessToken CreateToken(UserAccount user, IReadOnlyCollection<string> roles)
    {
        var ahora = _clock.UtcNow;
        var vence = ahora.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(SubjectClaim, user.Id),
            new(EmailClaim, user.Email),
            new(NameClaim, user.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };
        claims.AddRange(roles.Select(role => new Claim(RoleClaim, role)));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = ahora.UtcDateTime,
            NotBefore = ahora.UtcDateTime,
            Expires = vence.UtcDateTime,
            SigningCredentials = new SigningCredentials(SigningKey(_options), SecurityAlgorithms.HmacSha256)
        };

        return new AccessToken(new JsonWebTokenHandler().CreateToken(descriptor), vence);
    }
}
