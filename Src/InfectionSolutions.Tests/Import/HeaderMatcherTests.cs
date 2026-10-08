using InfectionSolutions.Application.Services.BulkImport;

namespace InfectionSolutions.Tests.Import;

public class HeaderMatcherTests
{
    [Theory]
    [InlineData("Código (SKU)", "codigosku")]
    [InlineData("  Teléfono Cliente ", "telefonocliente")]
    [InlineData("N° Factura", "nfactura")]
    public void Normalize_QuitaTildesEspaciosYSimbolos(string encabezado, string esperado)
    {
        Assert.Equal(esperado, HeaderMatcher.Normalize(encabezado));
    }

    [Fact]
    public void MatchAll_ReconoceSinonimos()
    {
        var campos = HeaderMatcher.MatchAll(["Ref", "Material", "Valor", "Existencias", "Cédula", "Celular", "Cant.", "Columna rara"]);

        Assert.Equal(
            [ImportField.Sku, ImportField.ProductName, ImportField.Price, ImportField.Stock, ImportField.Document, ImportField.Phone, ImportField.Quantity, null],
            campos);
    }

    [Fact]
    public void MatchAll_NombreConColumnaCliente_EsElProducto()
    {
        var campos = HeaderMatcher.MatchAll(["Cliente", "Nombre", "Precio"]);

        Assert.Equal(ImportField.ProductName, campos[1]);
    }

    [Fact]
    public void MatchAll_NombreEnHojaDeClientes_EsElCliente()
    {
        var campos = HeaderMatcher.MatchAll(["Documento", "Nombre", "Correo", "Edad"]);

        Assert.Equal(ImportField.CustomerName, campos[1]);
    }
}
