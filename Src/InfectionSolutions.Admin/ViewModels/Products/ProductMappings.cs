using InfectionSolutions.Application.Dtos.Products;

namespace InfectionSolutions.Admin.ViewModels.Products;

/// <summary>Conversion formulario &lt;-&gt; DTO de Application. Los controladores no tocan entidades.</summary>
public static class ProductMappings
{
    public static ProductRequest ToRequest(this ProductFormViewModel modelo) => new()
    {
        Sku = modelo.Sku,
        Name = modelo.Name,
        Description = modelo.Description,
        Category = modelo.Category,
        Unit = modelo.Unit,
        // Price y Stock son [Required]: aqui ya llegan validados.
        Price = modelo.Price!.Value,
        Stock = modelo.Stock!.Value,
        IsActive = modelo.IsActive
    };

    public static ProductFormViewModel ToFormViewModel(this ProductResponse product) => new()
    {
        Id = product.Id,
        Sku = product.Sku,
        Name = product.Name,
        Description = product.Description,
        Category = product.Category,
        Unit = product.Unit,
        Price = product.Price,
        Stock = product.Stock,
        IsActive = product.IsActive
    };
}
