using InfectionSolutions.Application.Common;

namespace InfectionSolutions.Tests.Application;

public class FieldRulesTests
{
    [Theory]
    [InlineData("Mampostería", "MAMPOSTERIA")]
    [InlineData("  Cémento gris ", "cemento GRIS")]
    [InlineData("Ñandú", "ñandu")]
    public void ComparisonKey_IgnoraMayusculasTildesYEspacios(string a, string b)
    {
        Assert.Equal(FieldRules.ComparisonKey(a), FieldRules.ComparisonKey(b));
    }

    [Fact]
    public void ComparisonKey_DistingueTextosDistintos()
    {
        Assert.NotEqual(FieldRules.ComparisonKey("Cementos"), FieldRules.ComparisonKey("Aceros"));
    }

    [Theory]
    [InlineData("1.020.458-796", "1020458796")]
    [InlineData("nit 900 123", "NIT900123")]
    public void NormalizeDocument_QuitaSeparadoresYUsaMayusculas(string entrada, string esperado)
    {
        Assert.Equal(esperado, FieldRules.NormalizeDocument(entrada));
    }
}
