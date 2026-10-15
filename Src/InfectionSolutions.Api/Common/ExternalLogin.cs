using System.Security.Cryptography;
using InfectionSolutions.Application.Dtos.Auth;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;

namespace InfectionSolutions.Api.Common;

/// <summary>Esquemas y opciones del inicio de sesion con Google y GitHub.</summary>
public static class ExternalLogin
{
    /// <summary>Cookie temporal donde el proveedor deja la identidad entre su respuesta y nuestro callback.</summary>
    public const string CookieScheme = "External";

    public const string Google = "Google";

    public const string GitHub = "GitHub";

    /// <summary>Esquema elegido, guardado en el estado del flujo para saber de que proveedor volvio.</summary>
    public const string ProviderItem = "proveedor";

    public static readonly IReadOnlyList<string> Providers = [Google, GitHub];
}

/// <summary>Seccion "Portal": a donde vuelve el navegador despues de Google/GitHub y de donde salen los enlaces de los correos.</summary>
public sealed class PortalOptions
{
    public const string SectionName = "Portal";

    public string BaseUrl { get; set; } = "http://localhost:4200";

    public string Url(string path) => $"{BaseUrl.TrimEnd('/')}{path}";
}

/// <summary>
/// El token no viaja en la URL de vuelta al portal (quedaria en el historial y en los logs):
/// el callback guarda la sesion bajo un codigo aleatorio de un solo uso que vence en dos
/// minutos, y el portal lo cambia por el token con POST /api/auth/external/exchange.
/// </summary>
public sealed class ExternalLoginCodes
{
    private static readonly TimeSpan Vigencia = TimeSpan.FromMinutes(2);

    private readonly IMemoryCache _cache;

    public ExternalLoginCodes(IMemoryCache cache)
    {
        _cache = cache;
    }

    public string Store(AuthResponse session)
    {
        var codigo = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        _cache.Set(Clave(codigo), session, Vigencia);
        return codigo;
    }

    public AuthResponse? Take(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || !_cache.TryGetValue(Clave(code), out AuthResponse? session))
        {
            return null;
        }

        _cache.Remove(Clave(code));
        return session;
    }

    private static string Clave(string codigo) => $"login-externo:{codigo}";
}
