using System.Security.Claims;
using InfectionSolutions.Domain.Identity;
using InfectionSolutions.Infrastructure.Security;

namespace InfectionSolutions.Api.Common;

public static class ClaimsPrincipalExtensions
{
    /// <summary>Id del usuario segun el token. Las rutas del portal nunca lo reciben en la peticion.</summary>
    public static string UserId(this ClaimsPrincipal user)
        => user.FindFirstValue(JwtTokenService.SubjectClaim)
            ?? throw new InvalidOperationException("El token no trae el claim sub.");

    public static bool IsAdministrator(this ClaimsPrincipal user) => user.IsInRole(ApplicationRoles.Administrator);
}
