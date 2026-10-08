using System.ComponentModel.DataAnnotations;

namespace InfectionSolutions.Admin.ViewModels.Account;

/// <summary>
/// Alta de un nuevo administrador desde el panel. Los clientes se registran
/// solos desde el portal (SPA) a traves de la API.
/// </summary>
public class RegisterAdminViewModel
{
    [Display(Name = "Nombre completo")]
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(120, ErrorMessage = "El nombre no puede superar los {1} caracteres.")]
    public string FullName { get; set; } = string.Empty;

    [Display(Name = "Correo electrónico")]
    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
    [StringLength(120, ErrorMessage = "El correo no puede superar los {1} caracteres.")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Contraseña")]
    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "La contraseña debe tener entre {2} y {1} caracteres.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Confirmar contraseña")]
    [Required(ErrorMessage = "Confirma la contraseña.")]
    [Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden.")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;
}
