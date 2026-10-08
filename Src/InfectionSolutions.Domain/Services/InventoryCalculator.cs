namespace InfectionSolutions.Domain.Services;

/// <summary>Subtotal sin IVA, IVA y total de una venta.</summary>
public readonly record struct SaleTotals(decimal Subtotal, decimal Tax, decimal Total);

/// <summary>
/// Calculos de una venta. Los precios del catalogo se guardan sin IVA y el
/// impuesto se suma al final, que es como se muestra en el recibo.
/// </summary>
public static class InventoryCalculator
{
    /// <summary>Tasa de IVA vigente: 19%.</summary>
    public const decimal TaxRate = 0.19m;

    public static decimal CalculateLineTotal(int quantity, decimal unitPrice)
    {
        if (quantity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "La cantidad no puede ser negativa.");
        }

        if (unitPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice), "El precio no puede ser negativo.");
        }

        return Redondear(quantity * unitPrice);
    }

    public static SaleTotals CalculateTotals(IEnumerable<(int Quantity, decimal UnitPrice)> lines)
    {
        var subtotal = Redondear(lines.Sum(line => CalculateLineTotal(line.Quantity, line.UnitPrice)));
        var tax = Redondear(subtotal * TaxRate);

        return new SaleTotals(subtotal, tax, subtotal + tax);
    }

    /// <summary>Stock restante; falla si no alcanza para la cantidad pedida.</summary>
    public static int DiscountStock(int stock, int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "La cantidad debe ser mayor que cero.");
        }

        if (quantity > stock)
        {
            throw new InvalidOperationException($"Stock insuficiente: disponible {stock}, solicitado {quantity}.");
        }

        return stock - quantity;
    }

    private static decimal Redondear(decimal valor) => decimal.Round(valor, 2, MidpointRounding.AwayFromZero);
}
