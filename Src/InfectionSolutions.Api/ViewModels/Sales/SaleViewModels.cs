using System.ComponentModel.DataAnnotations;

namespace InfectionSolutions.Api.ViewModels.Sales;

public sealed class SaleLineViewModel
{
    [Required(ErrorMessage = "El producto es obligatorio.")]
    public Guid? ProductId { get; set; }

    /// <example>3</example>
    [Required(ErrorMessage = "La cantidad es obligatoria.")]
    [Range(1, 100000, ErrorMessage = "La cantidad debe estar entre {1} y {2}.")]
    public int? Quantity { get; set; }
}

/// <summary>Venta registrada por un administrador para cualquier cliente activo.</summary>
public sealed class SaleViewModel
{
    [Required(ErrorMessage = "El cliente es obligatorio.")]
    public Guid? CustomerId { get; set; }

    [Required(ErrorMessage = "Agrega al menos un producto.")]
    [MinLength(1, ErrorMessage = "Agrega al menos un producto.")]
    [MaxLength(50, ErrorMessage = "Una venta admite máximo {1} productos distintos.")]
    public List<SaleLineViewModel> Lines { get; set; } = [];

    [StringLength(500, ErrorMessage = "Las notas no pueden superar los {1} caracteres.")]
    public string? Notes { get; set; }
}

/// <summary>Compra del cliente autenticado: el cliente sale del token.</summary>
public sealed class CheckoutViewModel
{
    [Required(ErrorMessage = "El carrito está vacío.")]
    [MinLength(1, ErrorMessage = "El carrito está vacío.")]
    [MaxLength(50, ErrorMessage = "Una compra admite máximo {1} productos distintos.")]
    public List<SaleLineViewModel> Lines { get; set; } = [];

    /// <example>Entregar en portería, horario de 7 a 4.</example>
    [StringLength(500, ErrorMessage = "Las notas no pueden superar los {1} caracteres.")]
    public string? Notes { get; set; }
}
