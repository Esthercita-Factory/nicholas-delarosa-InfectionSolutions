using InfectionSolutions.Application.Abstractions;
using InfectionSolutions.Application.Common;
using InfectionSolutions.Application.Dtos.Customers;
using InfectionSolutions.Application.Dtos.Products;
using InfectionSolutions.Application.Dtos.Sales;
using InfectionSolutions.Application.Services.Customers;
using InfectionSolutions.Application.Services.Products;
using InfectionSolutions.Application.Services.Sales;
using InfectionSolutions.Domain.Enums;
using Microsoft.Extensions.Options;

namespace InfectionSolutions.Application.Services.Exports;

public interface IExportService
{
    /// <summary>Exporta los productos que cumplen los filtros del listado (sin paginar).</summary>
    Task<ExportFile> ExportProductsAsync(ProductQuery query, ExportFormat format, CancellationToken cancellationToken = default);

    Task<ExportFile> ExportCustomersAsync(CustomerQuery query, ExportFormat format, CancellationToken cancellationToken = default);

    Task<ExportFile> ExportSalesAsync(SaleQuery query, ExportFormat format, CancellationToken cancellationToken = default);
}

public sealed class ExportService : IExportService
{
    private readonly IProductService _products;
    private readonly ICustomerService _customers;
    private readonly ISaleService _sales;
    private readonly IClock _clock;
    private readonly CompanyOptions _company;

    public ExportService(
        IProductService products,
        ICustomerService customers,
        ISaleService sales,
        IClock clock,
        IOptions<CompanyOptions> company)
    {
        _products = products;
        _customers = customers;
        _sales = sales;
        _clock = clock;
        _company = company.Value;
    }

    public async Task<ExportFile> ExportProductsAsync(ProductQuery query, ExportFormat format, CancellationToken cancellationToken = default)
    {
        var products = await _products.ListAllAsync(query, cancellationToken);

        var report = new TabularReport
        {
            Title = "Productos",
            Subtitle = DescribirFiltros(("búsqueda", query.Q), ("categoría", query.Category), ("estado", Estado(query.Active))),
            FileBaseName = "productos",
            Columns =
            [
                new("Código", Width: 1.2f),
                new("Producto", Width: 3f),
                new("Categoría", Width: 1.6f),
                new("Unidad", Width: 1.2f),
                new("Precio", ReportCellType.Money, 1.4f),
                new("Stock", ReportCellType.Integer, 0.8f),
                new("Estado", Width: 1f)
            ],
            Rows = products
                .Select(p => new object?[] { p.Sku, p.Name, p.Category, p.Unit, p.Price, p.Stock, p.IsActive ? "Activo" : "Inactivo" })
                .ToList()
        };

        return Escribir(report, format);
    }

    public async Task<ExportFile> ExportCustomersAsync(CustomerQuery query, ExportFormat format, CancellationToken cancellationToken = default)
    {
        var customers = await _customers.ListAllAsync(query, cancellationToken);

        var report = new TabularReport
        {
            Title = "Clientes",
            Subtitle = DescribirFiltros(("búsqueda", query.Q), ("estado", Estado(query.Active))),
            FileBaseName = "clientes",
            Columns =
            [
                new("Documento", Width: 1.3f),
                new("Nombre", Width: 2.4f),
                new("Edad", ReportCellType.Integer, 0.6f),
                new("Correo", Width: 2.4f),
                new("Teléfono", Width: 1.4f),
                new("Dirección", Width: 2.2f),
                new("Estado", Width: 0.9f)
            ],
            Rows = customers
                .Select(c => new object?[] { c.Document, c.FullName, c.Age, c.Email, c.Phone, c.Address, c.IsActive ? "Activo" : "Inactivo" })
                .ToList()
        };

        return Escribir(report, format);
    }

    public async Task<ExportFile> ExportSalesAsync(SaleQuery query, ExportFormat format, CancellationToken cancellationToken = default)
    {
        var sales = await _sales.ListAllAsync(query, cancellationToken);

        var report = new TabularReport
        {
            Title = "Ventas",
            Subtitle = DescribirFiltros(("búsqueda", query.Q), ("estado", query.Status is null ? null : EstadoEnTexto(query.Status.Value))),
            FileBaseName = "ventas",
            Columns =
            [
                new("Número", Width: 1.6f),
                new("Fecha", ReportCellType.DateTime, 1.4f),
                new("Cliente", Width: 2.4f),
                new("Documento", Width: 1.3f),
                new("Productos", ReportCellType.Integer, 0.9f),
                new("Total", ReportCellType.Money, 1.4f),
                new("Estado", Width: 1f)
            ],
            Rows = sales
                .Select(s => new object?[] { s.SaleNumber, s.SaleDate, s.CustomerName, s.CustomerDocument, s.ItemCount, s.Total, EstadoEnTexto(s.Status) })
                .ToList()
        };

        return Escribir(report, format);
    }

    private ExportFile Escribir(TabularReport report, ExportFormat format)
    {
        var ahora = _clock.UtcNow;
        var sufijo = DisplayFormats.ToBusinessTime(ahora).ToString("yyyyMMdd-HHmm");

        return format == ExportFormat.Pdf
            ? new ExportFile(PdfReportWriter.Write(report, _company, ahora), ExportFile.PdfContentType, $"{report.FileBaseName}-{sufijo}.pdf")
            : new ExportFile(ExcelReportWriter.Write(report, ahora), ExportFile.ExcelContentType, $"{report.FileBaseName}-{sufijo}.xlsx");
    }

    private static string? DescribirFiltros(params (string Nombre, string? Valor)[] filtros)
    {
        var aplicados = filtros
            .Where(f => !string.IsNullOrWhiteSpace(f.Valor))
            .Select(f => $"{f.Nombre} “{f.Valor!.Trim()}”")
            .ToList();

        return aplicados.Count == 0 ? null : $"Filtrado por {string.Join(", ", aplicados)}";
    }

    private static string? Estado(bool? activo) => activo switch { true => "activos", false => "inactivos", null => null };

    private static string EstadoEnTexto(SaleStatus status) => status == SaleStatus.Cancelled ? "Anulada" : "Completada";
}
