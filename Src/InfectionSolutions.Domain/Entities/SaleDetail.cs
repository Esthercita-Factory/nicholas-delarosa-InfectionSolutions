namespace InfectionSolutions.Domain.Entities;

public class SaleDetail
{
    public Guid Id { get; set; }
    public Guid SaleId { get; set; }
    public Sale Sale { get; set; } = null!;
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int Quantity { get; set; }

    /// <summary>Precio del producto al momento de la venta: si luego cambia, la venta no se altera.</summary>
    public decimal UnitPrice { get; set; }

    public decimal LineTotal { get; set; }
}
