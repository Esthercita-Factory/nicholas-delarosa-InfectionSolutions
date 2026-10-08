using InfectionSolutions.Domain.Services;

namespace InfectionSolutions.Tests.Domain;

public class SaleNumberGeneratorTests
{
    private static readonly DateTimeOffset Fecha = new(2026, 10, 7, 15, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Generate_UsaFechaYConsecutivoConCeros()
    {
        Assert.Equal("V-20261007-0012", SaleNumberGenerator.Generate(Fecha, 12));
    }

    [Fact]
    public void Generate_ComienzaConElPrefijoDelDia()
    {
        Assert.StartsWith(SaleNumberGenerator.DayPrefix(Fecha), SaleNumberGenerator.Generate(Fecha, 1));
    }

    [Fact]
    public void Generate_RechazaConsecutivoCero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SaleNumberGenerator.Generate(Fecha, 0));
    }
}
