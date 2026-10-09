using System.ComponentModel.DataAnnotations;

namespace InfectionSolutions.Infrastructure.Security;

/// <summary>Seccion "Jwt". La clave de firma nunca va en el repositorio fuera de Development.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = "InfectionSolutions.Api";

    [Required]
    public string Audience { get; set; } = "InfectionSolutions.Clients";

    /// <summary>HMAC-SHA256 necesita al menos 256 bits: 32 caracteres o mas.</summary>
    [Required(ErrorMessage = "Falta Jwt:SigningKey (configúrala con user-secrets o variable de entorno).")]
    [MinLength(32, ErrorMessage = "Jwt:SigningKey debe tener al menos 32 caracteres.")]
    public string SigningKey { get; set; } = string.Empty;

    [Range(5, 1440)]
    public int AccessTokenMinutes { get; set; } = 120;
}
