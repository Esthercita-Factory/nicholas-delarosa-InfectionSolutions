using InfectionSolutions.Application.Common;

namespace InfectionSolutions.Tests.Application;

public class InputParserTests
{
    [Theory]
    [InlineData("35", 35)]
    [InlineData(" 18 ", 18)]
    [InlineData("120", 120)]
    public void ParseAge_ConEnteroValido_DevuelveLaEdad(string entrada, int esperado)
    {
        var resultado = InputParser.ParseAge(entrada);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(esperado, resultado.Value);
    }

    [Theory]
    [InlineData("treinta")]
    [InlineData("25.5")]
    [InlineData("3O")]
    public void ParseAge_ConTextoNoEntero_CapturaFormatException(string entrada)
    {
        var resultado = InputParser.ParseAge(entrada);

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorCode.Validation, resultado.Error!.Code);
        Assert.Contains("número entero", resultado.Error.Message);
    }

    [Fact]
    public void ParseAge_ConNumeroGigante_CapturaOverflowException()
    {
        var resultado = InputParser.ParseAge("99999999999999");

        Assert.True(resultado.IsFailure);
        Assert.Contains("demasiado grande", resultado.Error!.Message);
    }

    [Theory]
    [InlineData("17")]
    [InlineData("121")]
    public void ParseAge_FueraDeRango_Falla(string entrada)
    {
        var resultado = InputParser.ParseAge(entrada);

        Assert.True(resultado.IsFailure);
        Assert.Equal("Age", resultado.Error!.Field);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseAge_Vacia_EsObligatoria(string? entrada)
    {
        var resultado = InputParser.ParseAge(entrada);

        Assert.True(resultado.IsFailure);
        Assert.Equal("La edad es obligatoria.", resultado.Error!.Message);
    }
}
