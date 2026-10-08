using InfectionSolutions.Application.Services.BulkImport;

namespace InfectionSolutions.Tests.Import;

public class ImportValueParserTests
{
    [Theory]
    [InlineData("32000", 32000)]
    [InlineData("$32.500", 32500)]
    [InlineData("1.250.000", 1250000)]
    [InlineData("1.250,50", 1250.50)]
    [InlineData("1,250.50", 1250.50)]
    [InlineData("15500.5", 15500.5)]
    [InlineData("12,75", 12.75)]
    [InlineData("$ 900", 900)]
    public void TryDecimal_EntiendeLosFormatosComunes(string texto, double esperado)
    {
        Assert.True(ImportValueParser.TryDecimal(texto, out var valor, out _));
        Assert.Equal((decimal)esperado, valor);
    }

    [Fact]
    public void TryDecimal_ConDoubleDeExcel_RedondeaADosDecimales()
    {
        Assert.True(ImportValueParser.TryDecimal(15500.499999, out var valor, out _));
        Assert.Equal(15500.50m, valor);
    }

    [Theory]
    [InlineData("gratis")]
    [InlineData("")]
    public void TryDecimal_ConTextoInvalido_DevuelveMensaje(string texto)
    {
        Assert.False(ImportValueParser.TryDecimal(texto, out _, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void TryInteger_RechazaDecimales()
    {
        Assert.False(ImportValueParser.TryInteger("2.5", out _, out var error));
        Assert.Contains("entero", error);
    }

    [Theory]
    [InlineData("15/10/2026")]
    [InlineData("2026-10-15")]
    [InlineData("15/10/2026 08:30")]
    public void TryDate_InterpretaLaFechaComoHoraDeColombia(string texto)
    {
        Assert.True(ImportValueParser.TryDate(texto, out var fecha, out _));
        Assert.Equal(TimeSpan.Zero, fecha.Offset);
        Assert.Equal(new DateOnly(2026, 10, 15), DateOnly.FromDateTime(fecha.ToOffset(TimeSpan.FromHours(-5)).DateTime));
    }

    [Fact]
    public void TryDate_ConNumeroDeSerieDeExcel()
    {
        var serial = new DateTime(2026, 10, 15).ToOADate();

        Assert.True(ImportValueParser.TryDate(serial, out var fecha, out _));
        Assert.Equal(new DateTimeOffset(2026, 10, 15, 5, 0, 0, TimeSpan.Zero), fecha);
    }

    [Fact]
    public void TryDate_ConTextoInvalido_Falla()
    {
        Assert.False(ImportValueParser.TryDate("ayer", out _, out var error));
        Assert.Contains("fecha válida", error);
    }
}
