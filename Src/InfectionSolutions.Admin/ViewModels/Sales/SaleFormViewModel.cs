using System.ComponentModel.DataAnnotations;
using InfectionSolutions.Application.Dtos.Customers;
using InfectionSolutions.Application.Dtos.Products;
using InfectionSolutions.Application.Dtos.Sales;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace InfectionSolutions.Admin.ViewModels.Sales;

public class SaleFormViewModel
{
    [Display(Name = "Cliente")]
    [Required(ErrorMessage = "Selecciona un cliente.")]
    public Guid? CustomerId { get; set; }

    [Display(Name = "Observaciones")]
    [StringLength(500, ErrorMessage = "Las observaciones no pueden superar los {1} caracteres.")]
    public string? Notes { get; set; }

    /// <summary>
    /// Lineas de la venta. Se enlazan con Lines.Index para que quitar una fila
    /// en el navegador no rompa la numeracion.
    /// </summary>
    public List<SaleLineViewModel> Lines { get; set; } = [];

    [ValidateNever]
    public IReadOnlyList<CustomerOption> Customers { get; set; } = [];

    [ValidateNever]
    public IReadOnlyList<ProductOption> Products { get; set; } = [];

    public SaleRequest ToRequest() => new()
    {
        CustomerId = CustomerId!.Value,
        Notes = Notes,
        Lines = Lines
            .Where(line => line.ProductId.HasValue)
            .Select(line => new SaleLineRequest(line.ProductId!.Value, line.Quantity ?? 0))
            .ToList()
    };
}

public class SaleLineViewModel
{
    [Required(ErrorMessage = "Selecciona un producto.")]
    public Guid? ProductId { get; set; }

    [Required(ErrorMessage = "Indica la cantidad.")]
    [Range(1, 100000, ErrorMessage = "La cantidad debe estar entre {1} y {2}.")]
    public int? Quantity { get; set; } = 1;
}
