using System.ComponentModel.DataAnnotations;

namespace InfectionSolutions.Api.ViewModels.Auth;

public sealed class LoginViewModel
{
    /// <example>admin@infection.com</example>
    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
    public string Email { get; set; } = string.Empty;

    /// <example>Admin123*</example>
    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    public string Password { get; set; } = string.Empty;
}

/// <summary>"Olvidé mi contraseña".</summary>
public sealed class ForgotPasswordViewModel
{
    /// <example>laura.mendoza@correo.com</example>
    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
    public string Email { get; set; } = string.Empty;
}

/// <summary>Nueva contraseña con el correo y el token que llegaron en el enlace.</summary>
public sealed class ResetPasswordViewModel
{
    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
    public string Email { get; set; } = string.Empty;

    /// <summary>El parámetro "token" del enlace, tal cual.</summary>
    [Required(ErrorMessage = "Falta el token del enlace.")]
    public string Token { get; set; } = string.Empty;

    /// <summary>Mínimo 8 caracteres con mayúscula, número y símbolo.</summary>
    /// <example>Nueva123*</example>
    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "La contraseña debe tener al menos {2} caracteres.")]
    public string Password { get; set; } = string.Empty;

    /// <example>Nueva123*</example>
    [Required(ErrorMessage = "Confirma la contraseña.")]
    [Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

/// <summary>Código de un solo uso que deja el inicio de sesión con Google o GitHub.</summary>
public sealed class ExternalCodeViewModel
{
    [Required(ErrorMessage = "Falta el código.")]
    public string Code { get; set; } = string.Empty;
}

/// <summary>Datos que faltan después de crear la cuenta con Google o GitHub (el correo es el de la cuenta).</summary>
public sealed class CompleteProfileViewModel
{
    /// <example>1020458796</example>
    [Required(ErrorMessage = "El documento es obligatorio.")]
    [StringLength(20, MinimumLength = 5, ErrorMessage = "El documento debe tener entre {2} y {1} caracteres.")]
    [RegularExpression(@"^[A-Za-z0-9]+$", ErrorMessage = "El documento solo admite letras y números, sin puntos ni espacios.")]
    public string Document { get; set; } = string.Empty;

    /// <example>Laura Mendoza</example>
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(120, ErrorMessage = "El nombre no puede superar los {1} caracteres.")]
    public string FullName { get; set; } = string.Empty;

    /// <example>34</example>
    [Required(ErrorMessage = "La edad es obligatoria.")]
    [Range(18, 120, ErrorMessage = "La edad debe estar entre {1} y {2} años.")]
    public int? Age { get; set; }

    /// <example>+57 310 555 0101</example>
    [Required(ErrorMessage = "El teléfono es obligatorio.")]
    [StringLength(20, MinimumLength = 7, ErrorMessage = "El teléfono debe tener entre {2} y {1} caracteres.")]
    [RegularExpression(@"^\+?[0-9\s\-()]{7,20}$", ErrorMessage = "El teléfono solo admite números, espacios, guiones, paréntesis y + al inicio.")]
    public string Phone { get; set; } = string.Empty;

    [StringLength(150, ErrorMessage = "La dirección no puede superar los {1} caracteres.")]
    public string? Address { get; set; }
}

/// <summary>Registro público de clientes del portal.</summary>
public sealed class RegisterViewModel
{
    /// <example>1020458796</example>
    [Required(ErrorMessage = "El documento es obligatorio.")]
    [StringLength(20, MinimumLength = 5, ErrorMessage = "El documento debe tener entre {2} y {1} caracteres.")]
    [RegularExpression(@"^[A-Za-z0-9]+$", ErrorMessage = "El documento solo admite letras y números, sin puntos ni espacios.")]
    public string Document { get; set; } = string.Empty;

    /// <example>Laura Mendoza</example>
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(120, ErrorMessage = "El nombre no puede superar los {1} caracteres.")]
    public string FullName { get; set; } = string.Empty;

    /// <example>34</example>
    [Required(ErrorMessage = "La edad es obligatoria.")]
    [Range(18, 120, ErrorMessage = "La edad debe estar entre {1} y {2} años.")]
    public int? Age { get; set; }

    /// <example>laura.mendoza@correo.com</example>
    [Required(ErrorMessage = "El correo es obligatorio.")]
    [StringLength(120, ErrorMessage = "El correo no puede superar los {1} caracteres.")]
    [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
    public string Email { get; set; } = string.Empty;

    /// <example>+57 310 555 0101</example>
    [Required(ErrorMessage = "El teléfono es obligatorio.")]
    [StringLength(20, MinimumLength = 7, ErrorMessage = "El teléfono debe tener entre {2} y {1} caracteres.")]
    [RegularExpression(@"^\+?[0-9\s\-()]{7,20}$", ErrorMessage = "El teléfono solo admite números, espacios, guiones, paréntesis y + al inicio.")]
    public string Phone { get; set; } = string.Empty;

    [StringLength(150, ErrorMessage = "La dirección no puede superar los {1} caracteres.")]
    public string? Address { get; set; }

    /// <summary>Mínimo 8 caracteres con mayúscula, número y símbolo.</summary>
    /// <example>Cliente123*</example>
    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "La contraseña debe tener al menos {2} caracteres.")]
    public string Password { get; set; } = string.Empty;

    /// <example>Cliente123*</example>
    [Required(ErrorMessage = "Confirma la contraseña.")]
    [Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
