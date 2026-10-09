using System.ComponentModel.DataAnnotations;

namespace InfectionSolutions.Api.ViewModels.Customers;

/// <summary>Cliente para crear o actualizar desde la administración.</summary>
public sealed class CustomerViewModel
{
    /// <summary>Cédula o NIT, solo letras y números.</summary>
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

    public bool IsActive { get; set; } = true;
}
