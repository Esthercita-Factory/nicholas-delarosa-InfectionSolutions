using InfectionSolutions.Domain.Services;

namespace InfectionSolutions.Tests.Domain;

public class InventoryCalculatorTests
{
    [Fact]
    public void CalculateTotals_SumaLineasYAgregaIva()
    {
        var totales = InventoryCalculator.CalculateTotals(new[]
        {
            (Quantity: 2, UnitPrice: 32_000m),
            (Quantity: 10, UnitPrice: 1_250.50m)
        });

        Assert.Equal(76_505.00m, totales.Subtotal);
        Assert.Equal(14_535.95m, totales.Tax);
        Assert.Equal(91_040.95m, totales.Total);
    }

    [Fact]
    public void CalculateTotals_SinLineas_DevuelveCero()
    {
        var totales = InventoryCalculator.CalculateTotals([]);

        Assert.Equal(new SaleTotals(0m, 0m, 0m), totales);
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(1, -10)]
    public void CalculateLineTotal_RechazaValoresNegativos(int cantidad, decimal precio)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => InventoryCalculator.CalculateLineTotal(cantidad, precio));
    }

    [Fact]
    public void DiscountStock_RestaLaCantidad()
    {
        Assert.Equal(7, InventoryCalculator.DiscountStock(10, 3));
    }

    [Fact]
    public void DiscountStock_FallaSiNoAlcanza()
    {
        Assert.Throws<InvalidOperationException>(() => InventoryCalculator.DiscountStock(2, 3));
    }
}
