using System.Text;
using InfectionSolutions.Application.Common;
using InfectionSolutions.Application.Dtos.Sales;
using InfectionSolutions.Application.Services.BulkImport;
using InfectionSolutions.Application.Services.Exports;
using InfectionSolutions.Application.Services.Receipts;
using InfectionSolutions.Domain.Enums;
using Microsoft.Extensions.Options;
using OfficeOpenXml;

namespace InfectionSolutions.Tests.Application;

public class DocumentGenerationTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 7, 15, 0, 0, TimeSpan.Zero);

    private static SaleResponse Venta(SaleStatus estado = SaleStatus.Completed) => new(
        Guid.NewGuid(), "V-20261007-0001", Ahora, estado, Guid.NewGuid(),
        "Laura Mendoza", "1020458796", "laura@correo.com", "3001234567", "Calle 100 # 15-20",
        64_000m, 12_160m, 76_160m, "Entrega en obra", null,
        [new SaleDetailResponse(Guid.NewGuid(), "CEM-50", "Cemento gris 50 kg", "Bulto", 2, 32_000m, 64_000m)]);

    private static bool EsPdf(byte[] contenido) => contenido.Length > 1000 && Encoding.ASCII.GetString(contenido, 0, 5) == "%PDF-";

    [Theory]
    [InlineData(SaleStatus.Completed)]
    [InlineData(SaleStatus.Cancelled)]
    public void Recibo_GeneraUnPdf(SaleStatus estado)
    {
        var servicio = new ReceiptService(Substitute.For<ISaleRepository>(), Substitute.For<IReceiptStorage>(), Options.Create(new CompanyOptions()));

        Assert.True(EsPdf(servicio.Generate(Venta(estado))));
    }

    [Fact]
    public async Task Recibo_SiNoEstaGuardado_SeGeneraYSeGuarda()
    {
        var cliente = new Customer { FullName = "Laura", Document = "1020458796", Email = "l@c.com", Phone = "3000000000" };
        var producto = new Product { Sku = "CEM-50", Name = "Cemento", Unit = "Bulto" };
        var venta = new Sale
        {
            Id = Guid.NewGuid(), SaleNumber = "V-20261007-0002", SaleDate = Ahora, Customer = cliente,
            Subtotal = 100m, Tax = 19m, Total = 119m,
            Details = [new SaleDetail { Product = producto, Quantity = 1, UnitPrice = 100m, LineTotal = 100m }]
        };
        var ventas = Substitute.For<ISaleRepository>();
        ventas.FindByIdAsync(venta.Id, Arg.Any<CancellationToken>()).Returns(venta);
        var almacen = Substitute.For<IReceiptStorage>();
        almacen.ReadAsync(default!, default).ReturnsForAnyArgs((byte[]?)null);

        var servicio = new ReceiptService(ventas, almacen, Options.Create(new CompanyOptions()));
        var resultado = await servicio.GetAsync(venta.Id);

        Assert.True(resultado.IsSuccess);
        Assert.Equal("V-20261007-0002.pdf", resultado.Value!.FileName);
        Assert.True(EsPdf(resultado.Value.Content));
        await almacen.Received(1).SaveAsync("V-20261007-0002.pdf", Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
    }

    private static TabularReport Reporte() => new()
    {
        Title = "Productos",
        FileBaseName = "productos",
        Columns = [new("Código"), new("Producto"), new("Precio", ReportCellType.Money), new("Stock", ReportCellType.Integer), new("Fecha", ReportCellType.DateTime)],
        Rows =
        [
            ["CEM-50", "Cemento gris 50 kg", 32_000m, 450, Ahora],
            ["VAR-12", "Varilla 1/2", 15_500.50m, 800, Ahora]
        ]
    };

    [Fact]
    public void Excel_TieneTituloEncabezadosYValoresTipados()
    {
        var bytes = ExcelReportWriter.Write(Reporte(), Ahora);

        using var package = new ExcelPackage(new MemoryStream(bytes));
        var hoja = package.Workbook.Worksheets[0];
        Assert.Equal("Productos", hoja.Cells[1, 1].Text);
        Assert.Equal("Código", hoja.Cells[ExcelReportWriter.HeaderRow, 1].Text);
        Assert.Equal(32_000d, Convert.ToDouble(hoja.Cells[ExcelReportWriter.HeaderRow + 1, 3].Value));
        // La fecha se escribe en hora de Colombia.
        Assert.Equal(new DateTime(2026, 10, 7, 10, 0, 0), hoja.Cells[ExcelReportWriter.HeaderRow + 1, 5].GetValue<DateTime>());
    }

    [Fact]
    public void Excel_ExportadoSePuedeVolverAImportar()
    {
        var bytes = ExcelReportWriter.Write(Reporte(), Ahora);
        var resultado = new ImportResult();

        var hojas = ExcelImportReader.Read(new MemoryStream(bytes), resultado);
        var datos = ImportNormalizer.Normalize(hojas, resultado);

        Assert.Equal(0, resultado.ErrorCount);
        Assert.Equal(2, datos.Products.Count);
        Assert.Equal(15_500.50m, datos.Products["VAR-12"].Price);
    }

    [Fact]
    public void Pdf_DeReporte_SeGenera()
    {
        Assert.True(EsPdf(PdfReportWriter.Write(Reporte(), new CompanyOptions(), Ahora)));
    }
}
