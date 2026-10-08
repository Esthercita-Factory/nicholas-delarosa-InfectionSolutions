using InfectionSolutions.Application.Services.BulkImport;
using InfectionSolutions.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using OfficeOpenXml;

namespace InfectionSolutions.Tests.Import;

/// <summary>Flujo completo con un .xlsx real en memoria y repositorios falsos.</summary>
public class BulkImportServiceTests
{
    private readonly IProductRepository _products = Substitute.For<IProductRepository>();
    private readonly ICustomerRepository _customers = Substitute.For<ICustomerRepository>();
    private readonly ISaleRepository _sales = Substitute.For<ISaleRepository>();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FixedClock _clock = new(new DateTimeOffset(2026, 10, 7, 15, 0, 0, TimeSpan.Zero));
    private readonly BulkImportService _service;

    private readonly List<Product> _productosAgregados = [];
    private readonly List<Customer> _clientesAgregados = [];
    private readonly List<Sale> _ventasAgregadas = [];

    public BulkImportServiceTests()
    {
        _service = new BulkImportService(_products, _customers, _sales, _unitOfWork, _clock, NullLogger<BulkImportService>.Instance);

        _products.FindBySkusAsync(default!, default).ReturnsForAnyArgs([]);
        _products.GetCategoriesAsync(default).ReturnsForAnyArgs([]);
        _customers.FindByDocumentsAsync(default!, default).ReturnsForAnyArgs([]);
        _customers.FindByEmailsAsync(default!, default).ReturnsForAnyArgs([]);
        _sales.FindExistingNumbersAsync(default!, default).ReturnsForAnyArgs([]);

        _products.When(r => r.AddAsync(Arg.Any<Product>(), Arg.Any<CancellationToken>())).Do(c => _productosAgregados.Add(c.Arg<Product>()));
        _customers.When(r => r.AddAsync(Arg.Any<Customer>(), Arg.Any<CancellationToken>())).Do(c => _clientesAgregados.Add(c.Arg<Customer>()));
        _sales.When(r => r.AddAsync(Arg.Any<Sale>(), Arg.Any<CancellationToken>())).Do(c => _ventasAgregadas.Add(c.Arg<Sale>()));
    }

    [Fact]
    public async Task ImportarLaPlantilla_CreaProductosClientesYVentas()
    {
        using var excel = new MemoryStream(_service.BuildTemplate());

        var resultado = await _service.ImportAsync(excel, "plantilla.xlsx", "admin-id");

        Assert.True(resultado.Saved);
        Assert.Equal(0, resultado.ErrorCount);
        Assert.Equal(4, resultado.ProductsCreated);
        Assert.Equal(2, resultado.CustomersCreated);
        Assert.Equal(2, resultado.SalesCreated);
        Assert.True(_unitOfWork.Committed);

        // El stock sale de la hoja Inventario; las ventas historicas no lo descuentan.
        Assert.Equal(450, _productosAgregados.Single(p => p.Sku == "CEM-50").Stock);
        Assert.Equal("Bulto", _productosAgregados.Single(p => p.Sku == "CEM-50").Unit);

        var fac1001 = _ventasAgregadas.Single(v => v.SaleNumber == "FAC-1001");
        Assert.Equal(2, fac1001.Details.Count);
        Assert.Equal(707_512.50m, fac1001.Subtotal);
        Assert.Equal(fac1001.Subtotal + fac1001.Tax, fac1001.Total);
        Assert.Equal(SaleStatus.Completed, fac1001.Status);
        Assert.Equal("admin-id", fac1001.CreatedByUserId);
    }

    [Fact]
    public async Task ProductoExistente_SoloActualizaLosDatosQueTraeElArchivo()
    {
        var existente = new Product { Sku = "CEM-50", Name = "Cemento viejo", Category = "Cementos", Unit = "Bulto", Price = 30000m, Stock = 5, Description = "Original" };
        _products.FindBySkusAsync(default!, default).ReturnsForAnyArgs([existente]);

        using var excel = Libro(["Código", "Precio", "Stock"], ["cem-50", 32000, 80]);

        var resultado = await _service.ImportAsync(excel, "precios.xlsx", null);

        Assert.Equal((0, 1), (resultado.ProductsCreated, resultado.ProductsUpdated));
        Assert.Equal((32000m, 80, "Cemento viejo", "Original"), (existente.Price, existente.Stock, existente.Name, existente.Description));
    }

    [Fact]
    public async Task ClienteNuevoIncompleto_NoSeCreaYSeReporta()
    {
        using var excel = Libro(["Documento", "Cliente", "Correo"], ["1020458796", "Laura", "laura@correo.com"]);

        var resultado = await _service.ImportAsync(excel, "clientes.xlsx", null);

        Assert.Equal(0, resultado.CustomersCreated);
        var error = Assert.Single(resultado.Log, e => e.Level == ImportLogLevel.Error);
        Assert.Contains("teléfono, edad", error.Message);
    }

    [Fact]
    public async Task CorreoDeOtroCliente_NoSeGuarda()
    {
        var otro = new Customer { Document = "999999999", Email = "laura@correo.com" };
        _customers.FindByEmailsAsync(default!, default).ReturnsForAnyArgs([otro]);

        using var excel = Libro(["Documento", "Cliente", "Correo", "Teléfono", "Edad"], ["1020458796", "Laura", "laura@correo.com", "3001234567", 30]);

        var resultado = await _service.ImportAsync(excel, "clientes.xlsx", null);

        Assert.Equal(0, resultado.CustomersCreated);
        Assert.Contains(resultado.Log, e => e.Level == ImportLogLevel.Error && e.Message.Contains("ya pertenece al cliente 999999999"));
    }

    [Fact]
    public async Task VentaConProductoInexistente_NoSeRegistraNiAMedias()
    {
        var cliente = new Customer { Document = "1020458796", FullName = "Laura", Email = "l@c.com" };
        var cemento = new Product { Sku = "CEM-50", Name = "Cemento", Price = 32000m };
        _customers.FindByDocumentsAsync(default!, default).ReturnsForAnyArgs([cliente]);
        _products.FindBySkusAsync(default!, default).ReturnsForAnyArgs(c => c.Arg<IReadOnlyCollection<string>>().Contains("CEM-50") ? new List<Product> { cemento } : new List<Product>());

        using var excel = Libro(["Factura", "Documento", "Código", "Cantidad"],
            ["FAC-9", "1020458796", "CEM-50", 2],
            ["FAC-9", "1020458796", "NO-EXISTE", 1]);

        var resultado = await _service.ImportAsync(excel, "ventas.xlsx", null);

        Assert.Equal(0, resultado.SalesCreated);
        Assert.Empty(_ventasAgregadas);
        Assert.Contains(resultado.Log, e => e.Level == ImportLogLevel.Error && e.Message.Contains("NO-EXISTE"));
    }

    [Fact]
    public async Task VentaYaRegistrada_SeOmite()
    {
        var cliente = new Customer { Document = "1020458796" };
        var cemento = new Product { Sku = "CEM-50", Price = 32000m };
        _customers.FindByDocumentsAsync(default!, default).ReturnsForAnyArgs([cliente]);
        _products.FindBySkusAsync(default!, default).ReturnsForAnyArgs([cemento]);
        _sales.FindExistingNumbersAsync(default!, default).ReturnsForAnyArgs(["FAC-1"]);

        using var excel = Libro(["Factura", "Documento", "Código", "Cantidad"], ["FAC-1", "1020458796", "CEM-50", 2]);

        var resultado = await _service.ImportAsync(excel, "ventas.xlsx", null);

        Assert.Equal(0, resultado.SalesCreated);
        Assert.Contains(resultado.Log, e => e.Level == ImportLogLevel.Warning && e.Message.Contains("ya estaba registrada"));
    }

    [Fact]
    public async Task CategoriaEscritaDistinto_ReutilizaLaRegistrada()
    {
        _products.GetCategoriesAsync(default).ReturnsForAnyArgs(["Mampostería"]);

        using var excel = Libro(["Código", "Producto", "Categoría", "Precio"],
            ["LAD-1", "Ladrillo", "MAMPOSTERIA", 900],
            ["BLO-5", "Bloque", "mampostería ", 1500]);

        var resultado = await _service.ImportAsync(excel, "categorias.xlsx", null);

        Assert.Equal(2, resultado.ProductsCreated);
        Assert.All(_productosAgregados, p => Assert.Equal("Mampostería", p.Category));
    }

    [Fact]
    public async Task ArchivoQueNoEsExcel_DevuelveErrorSinTocarLaBase()
    {
        using var basura = new MemoryStream("esto no es un excel"u8.ToArray());

        var resultado = await _service.ImportAsync(basura, "falso.xlsx", null);

        Assert.False(resultado.Saved);
        Assert.Contains("no es un Excel", Assert.Single(resultado.Log).Message);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    private static MemoryStream Libro(string[] encabezados, params object[][] filas)
    {
        InfectionSolutions.Application.Common.DocumentLibraries.EnsureConfigured();
        using var package = new ExcelPackage();
        var hoja = package.Workbook.Worksheets.Add("Datos");

        for (var c = 0; c < encabezados.Length; c++)
        {
            hoja.Cells[1, c + 1].Value = encabezados[c];
        }

        for (var r = 0; r < filas.Length; r++)
        {
            for (var c = 0; c < filas[r].Length; c++)
            {
                hoja.Cells[r + 2, c + 1].Value = filas[r][c];
            }
        }

        return new MemoryStream(package.GetAsByteArray());
    }
}
