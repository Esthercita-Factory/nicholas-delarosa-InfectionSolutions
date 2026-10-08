using System.ComponentModel.DataAnnotations;

namespace InfectionSolutions.Admin.ViewModels.Customers;

public class CustomerFormViewModel
{
    public Guid Id { get; set; }

    [Display(Name = "Documento")]
    [Required(ErrorMessage = "El documento es obligatorio.")]
    [StringLength(20, MinimumLength = 5, ErrorMessage = "El documento debe tener entre {2} y {1} caracteres.")]
    [RegularExpression(@"^[A-Za-z0-9]+$", ErrorMessage = "El documento solo admite letras y números, sin puntos ni espacios.")]
    public string Document { get; set; } = string.Empty;

    [Display(Name = "Nombre completo")]
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(120, ErrorMessage = "El nombre no puede superar los {1} caracteres.")]
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Texto a proposito: se convierte con int.Parse dentro de un try-catch
    /// (InputParser) para mostrar un mensaje amigable si no es un entero.
    /// </summary>
    [Display(Name = "Edad")]
    [Required(ErrorMessage = "La edad es obligatoria.")]
    public string? Age { get; set; }

    [Display(Name = "Correo electrónico")]
    [Required(ErrorMessage = "El correo es obligatorio.")]
    [StringLength(120, ErrorMessage = "El correo no puede superar los {1} caracteres.")]
    [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Teléfono")]
    [Required(ErrorMessage = "El teléfono es obligatorio.")]
    [StringLength(20, MinimumLength = 7, ErrorMessage = "El teléfono debe tener entre {2} y {1} caracteres.")]
    [RegularExpression(@"^\+?[0-9\s\-()]{7,20}$", ErrorMessage = "El teléfono solo admite números, espacios, guiones, paréntesis y + al inicio.")]
    public string Phone { get; set; } = string.Empty;

    [Display(Name = "Dirección")]
    [StringLength(150, ErrorMessage = "La dirección no puede superar los {1} caracteres.")]
    public string? Address { get; set; }

    [Display(Name = "Activo")]
    public bool IsActive { get; set; } = true;
}
