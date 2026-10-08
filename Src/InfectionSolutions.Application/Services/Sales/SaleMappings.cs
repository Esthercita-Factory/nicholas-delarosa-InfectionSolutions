using InfectionSolutions.Application.Dtos.Sales;
using InfectionSolutions.Domain.Entities;

namespace InfectionSolutions.Application.Services.Sales;

/// <summary>Entidad Sale -> DTO. La usan las ventas, los recibos y las exportaciones.</summary>
internal static class SaleMappings
{
    public static SaleListItemResponse ToListItem(Sale sale) => new(
        sale.Id,
        sale.SaleNumber,
        sale.SaleDate,
        sale.Customer.FullName,
        sale.Customer.Document,
        sale.Details.Count,
        sale.Total,
        sale.Status);

    /// <summary>Requiere el cliente y los detalles con su producto cargados.</summary>
    public static SaleResponse ToResponse(Sale sale) => new(
        sale.Id,
        sale.SaleNumber,
        sale.SaleDate,
        sale.Status,
        sale.CustomerId,
        sale.Customer.FullName,
        sale.Customer.Document,
        sale.Customer.Email,
        sale.Customer.Phone,
        sale.Customer.Address,
        sale.Subtotal,
        sale.Tax,
        sale.Total,
        sale.Notes,
        sale.CancelledAt,
        sale.Details
            .Select(d => new SaleDetailResponse(d.ProductId, d.Product.Sku, d.Product.Name, d.Product.Unit, d.Quantity, d.UnitPrice, d.LineTotal))
            .ToList());
}
