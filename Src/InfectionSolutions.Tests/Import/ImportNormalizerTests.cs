using InfectionSolutions.Application.Services.BulkImport;

namespace InfectionSolutions.Tests.Import;

public class ImportNormalizerTests
{
    private static RawRow Fila(int numero, params (ImportField Campo, object? Valor)[] valores)
        => new(numero, valores.ToDictionary(v => v.Campo, v => v.Valor));

    private static (NormalizedImport Datos, ImportResult Resultado) Normalizar(params RawRow[] filas)
    {
        var resultado = new ImportResult();
        var datos = ImportNormalizer.Normalize([new RawSheet("Ventas", filas)], resultado);
        return (datos, resultado);
    }

    [Fact]
    public void FilaMezclada_SeSeparaEnProductoClienteYLineaDeVenta()
    {
        var (datos, resultado) = Normalizar(Fila(5,
            (ImportField.SaleNumber, "fac-1"),
            (ImportField.Document, "1.020.458.796"),
            (ImportField.CustomerName, "Laura Mendoza"),
            (ImportField.Email, "Laura@Correo.com"),
            (ImportField.Sku, "cem-50"),
            (ImportField.ProductName, "Cemento"),
            (ImportField.Price, 32000d),
            (ImportField.Quantity, 3d)));

        Assert.Equal(0, resultado.ErrorCount);
        Assert.Equal("Cemento", datos.Products["CEM-50"].Name);
        Assert.Equal(32000m, datos.Products["CEM-50"].Price);
        Assert.Equal("laura@correo.com", datos.Customers["1020458796"].Email);

        var linea = Assert.Single(datos.SaleLines);
        Assert.Equal(("FAC-1", "1020458796", "CEM-50", 3), (linea.SaleNumber, linea.Document, linea.Sku, linea.Quantity));
    }

    [Fact]
    public void FilasRepetidas_SeFusionanEnUnSoloProductoYCliente()
    {
        var (datos, resultado) = Normalizar(
            Fila(5, (ImportField.Sku, "CEM-50"), (ImportField.ProductName, "Cemento"), (ImportField.Document, "1020458796"), (ImportField.Quantity, 1d)),
            Fila(6, (ImportField.Sku, "CEM-50"), (ImportField.Stock, 40d), (ImportField.Document, "1020458796"), (ImportField.Phone, "3001234567"), (ImportField.Quantity, 2d)));

        Assert.Single(datos.Products);
        Assert.Equal(("Cemento", 40), (datos.Products["CEM-50"].Name, datos.Products["CEM-50"].Stock));
        Assert.Equal("3001234567", Assert.Single(datos.Customers).Value.Phone);
        Assert.Equal(2, datos.SaleLines.Count);
        Assert.Equal(0, resultado.WarningCount);
    }

    [Fact]
    public void ValorDistintoEntreFilas_GeneraAdvertenciaYUsaElUltimo()
    {
        var (datos, resultado) = Normalizar(
            Fila(5, (ImportField.Sku, "CEM-50"), (ImportField.Price, 30000d)),
            Fila(6, (ImportField.Sku, "CEM-50"), (ImportField.Price, 32000d)));

        Assert.Equal(32000m, datos.Products["CEM-50"].Price);
        var advertencia = Assert.Single(resultado.Log, e => e.Level == ImportLogLevel.Warning);
        Assert.Equal(6, advertencia.Row);
    }

    [Fact]
    public void ProductoSinCodigo_EsError()
    {
        var (datos, resultado) = Normalizar(Fila(7, (ImportField.ProductName, "Cemento"), (ImportField.Price, 100d)));

        Assert.Empty(datos.Products);
        var error = Assert.Single(resultado.Log, e => e.Level == ImportLogLevel.Error);
        Assert.Equal((7, "Código"), (error.Row!.Value, error.Field));
    }

    [Fact]
    public void PrecioInvalido_NoActualizaElProductoPeroLaVentaConservaElCodigo()
    {
        var (datos, resultado) = Normalizar(Fila(8,
            (ImportField.Sku, "CEM-50"),
            (ImportField.Price, "gratis"),
            (ImportField.Document, "1020458796"),
            (ImportField.Quantity, 1d)));

        Assert.Empty(datos.Products);
        Assert.Equal("CEM-50", Assert.Single(datos.SaleLines).Sku);
        Assert.Contains(resultado.Log, e => e.Level == ImportLogLevel.Error && e.Field == "Precio");
    }

    [Theory]
    [InlineData("correo-malo", ImportField.Email)]
    [InlineData("abc", ImportField.Phone)]
    public void DatoDeClienteInvalido_EsError(string valor, ImportField campo)
    {
        var (datos, resultado) = Normalizar(Fila(9, (ImportField.Document, "1020458796"), (campo, valor)));

        Assert.Empty(datos.Customers);
        Assert.Equal(1, resultado.ErrorCount);
    }

    [Fact]
    public void EdadFueraDeRango_EsError()
    {
        var (_, resultado) = Normalizar(Fila(9, (ImportField.Document, "1020458796"), (ImportField.Age, 15d)));

        Assert.Contains(resultado.Log, e => e.Level == ImportLogLevel.Error && e.Field == "Edad");
    }

    [Fact]
    public void LineaDeVentaSinCliente_EsError()
    {
        var (datos, resultado) = Normalizar(Fila(10, (ImportField.Sku, "CEM-50"), (ImportField.Quantity, 2d)));

        Assert.Empty(datos.SaleLines);
        Assert.Contains(resultado.Log, e => e.Level == ImportLogLevel.Error && e.Field == "Documento");
    }

    [Fact]
    public void DatosDeVentaSinCantidad_SoloAdvierte()
    {
        var (datos, resultado) = Normalizar(Fila(11, (ImportField.Sku, "CEM-50"), (ImportField.SaleNumber, "FAC-1")));

        Assert.Empty(datos.SaleLines);
        Assert.Equal(0, resultado.ErrorCount);
        Assert.Equal(1, resultado.WarningCount);
    }
}
