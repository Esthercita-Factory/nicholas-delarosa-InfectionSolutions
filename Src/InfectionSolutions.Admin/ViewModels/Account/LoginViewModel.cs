using System.ComponentModel.DataAnnotations;

namespace InfectionSolutions.Admin.ViewModels.Account;

public class LoginViewModel
{
    [Display(Name = "Correo electrónico")]
    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Contraseña")]
    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Mantener la sesión iniciada")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}
