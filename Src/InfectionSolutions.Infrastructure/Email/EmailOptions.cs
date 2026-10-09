namespace InfectionSolutions.Infrastructure.Email;

/// <summary>
/// Seccion "Email". Cambiar de Gmail a un SMTP empresarial es solo cambiar
/// Smtp:Host, Port, Security y las credenciales: el codigo no se toca.
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>"Smtp" envia de verdad; "Log" solo escribe el correo en el log (desarrollo sin credenciales).</summary>
    public string Provider { get; set; } = "Smtp";

    /// <summary>Remitente. Vacio = el usuario SMTP (Gmail no deja enviar como otra direccion sin configurarla).</summary>
    public string? FromAddress { get; set; }

    public string FromName { get; set; } = "InfectionSolutions";

    public SmtpSettings Smtp { get; set; } = new();

    public bool SmtpConfigured =>
        !string.IsNullOrWhiteSpace(Smtp.Host)
        && !string.IsNullOrWhiteSpace(Smtp.UserName)
        && !string.IsNullOrWhiteSpace(Smtp.Password);
}

public sealed class SmtpSettings
{
    public string Host { get; set; } = "smtp.gmail.com";

    public int Port { get; set; } = 587;

    /// <summary>StartTls (587), SslOnConnect (465) o None (servidores internos de prueba).</summary>
    public string Security { get; set; } = "StartTls";

    public string? UserName { get; set; }

    /// <summary>En Gmail, una contraseña de aplicacion (no la clave de la cuenta).</summary>
    public string? Password { get; set; }

    public int TimeoutSeconds { get; set; } = 30;
}
