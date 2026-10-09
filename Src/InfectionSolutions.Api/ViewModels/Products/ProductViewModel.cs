using System.ComponentModel.DataAnnotations;

namespace InfectionSolutions.Api.ViewModels.Products;

/// <summary>Producto para crear o actualizar. El precio va sin IVA (el 19 % se suma en la venta).</summary>
public sealed class ProductViewModel
{
    /// <summary>Código único. Se guarda en mayúsculas.</summary>
    /// <example>CEM-ARG-50</example>
    [Required(ErrorMessage = "El código es obligatorio.")]
    [StringLength(30, ErrorMessage = "El código no puede superar los {1} caracteres.")]
    [RegularExpression(@"^[A-Za-z0-9\-_.]+$", ErrorMessage = "El código solo admite letras, números, guiones y puntos.")]
    public string Sku { get; set; } = string.Empty;

    /// <example>Cemento gris Argos 50 kg</example>
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(120, ErrorMessage = "El nombre no puede superar los {1} caracteres.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "La descripción no puede superar los {1} caracteres.")]
    public string? Description { get; set; }

    /// <summary>Si ya existe escrita distinto (mayúsculas o tildes) se reutiliza la registrada.</summary>
    /// <example>Cementos</example>
    [Required(ErrorMessage = "La categoría es obligatoria.")]
    [StringLength(80, ErrorMessage = "La categoría no puede superar los {1} caracteres.")]
    public string Category { get; set; } = string.Empty;

    /// <example>Bulto</example>
    [Required(ErrorMessage = "La unidad de venta es obligatoria.")]
    [StringLength(30, ErrorMessage = "La unidad no puede superar los {1} caracteres.")]
    public string Unit { get; set; } = "Unidad";

    /// <summary>Precio unitario sin IVA.</summary>
    /// <example>32500</example>
    [Required(ErrorMessage = "El precio es obligatorio.")]
    [Range(0.01, 999999999999.99, ErrorMessage = "El precio debe ser mayor que cero y no superar {2}.")]
    public decimal? Price { get; set; }

    /// <example>120</example>
    [Required(ErrorMessage = "El stock es obligatorio.")]
    [Range(0, int.MaxValue, ErrorMessage = "El stock no puede ser negativo.")]
    public int? Stock { get; set; }

    /// <summary>Inactivo = oculto del catálogo del portal.</summary>
    public bool IsActive { get; set; } = true;
}
