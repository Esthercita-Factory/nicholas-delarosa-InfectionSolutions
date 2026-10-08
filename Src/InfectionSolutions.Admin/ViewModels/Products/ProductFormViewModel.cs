using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace InfectionSolutions.Admin.ViewModels.Products;

/// <summary>
/// Campos editables de un producto. El id, las fechas de auditoria y el
/// formato del codigo los maneja el servicio.
/// </summary>
public class ProductFormViewModel
{
    public Guid Id { get; set; }

    [Display(Name = "Código (SKU)")]
    [Required(ErrorMessage = "El código es obligatorio.")]
    [StringLength(30, ErrorMessage = "El código no puede superar los {1} caracteres.")]
    [RegularExpression(@"^[A-Za-z0-9\-_.]+$", ErrorMessage = "El código solo admite letras, números, guiones y puntos.")]
    public string Sku { get; set; } = string.Empty;

    [Display(Name = "Nombre")]
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(120, ErrorMessage = "El nombre no puede superar los {1} caracteres.")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Descripción")]
    [StringLength(1000, ErrorMessage = "La descripción no puede superar los {1} caracteres.")]
    public string? Description { get; set; }

    [Display(Name = "Categoría")]
    [Required(ErrorMessage = "La categoría es obligatoria.")]
    [StringLength(80, ErrorMessage = "La categoría no puede superar los {1} caracteres.")]
    public string Category { get; set; } = string.Empty;

    [Display(Name = "Unidad de venta")]
    [Required(ErrorMessage = "La unidad de venta es obligatoria.")]
    [StringLength(30, ErrorMessage = "La unidad no puede superar los {1} caracteres.")]
    public string Unit { get; set; } = "Unidad";

    // Nullable para que el campo vacio falle en [Required] en lugar de llegar como 0.
    [Display(Name = "Precio unitario (sin IVA)")]
    [Required(ErrorMessage = "El precio es obligatorio.")]
    [Range(0.01, 999999999999.99, ErrorMessage = "El precio debe ser mayor que cero y no superar {2}.")]
    public decimal? Price { get; set; }

    [Display(Name = "Stock")]
    [Required(ErrorMessage = "El stock es obligatorio.")]
    [Range(0, int.MaxValue, ErrorMessage = "El stock no puede ser negativo.")]
    public int? Stock { get; set; }

    [Display(Name = "Activo (visible en el catálogo)")]
    public bool IsActive { get; set; } = true;

    /// <summary>Sugerencias para el campo categoria; las carga el controlador.</summary>
    [ValidateNever]
    public IReadOnlyList<string> Categories { get; set; } = [];

    public static readonly IReadOnlyList<string> Units =
        ["Unidad", "Bulto", "Metro", "Metro cuadrado", "Metro cúbico", "Kilogramo", "Tonelada", "Galón", "Litro", "Rollo", "Caja"];
}
