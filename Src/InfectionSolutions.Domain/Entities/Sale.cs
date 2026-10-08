using InfectionSolutions.Domain.Enums;

namespace InfectionSolutions.Domain.Entities;

public class Sale
{
    public Guid Id { get; set; }
    public string SaleNumber { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public DateTimeOffset SaleDate { get; set; }
    public SaleStatus Status { get; set; } = SaleStatus.Completed;

    /// <summary>Suma de las lineas, sin IVA.</summary>
    public decimal Subtotal { get; set; }

    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public string? Notes { get; set; }

    /// <summary>Usuario que registro la venta (admin desde el panel o cliente desde el portal).</summary>
    public string? CreatedByUserId { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }
    public ICollection<SaleDetail> Details { get; set; } = new List<SaleDetail>();
}
