using InfectionSolutions.Application.Abstractions;
using InfectionSolutions.Application.Common;
using InfectionSolutions.Application.Dtos.Sales;
using InfectionSolutions.Application.Services.Sales;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;

namespace InfectionSolutions.Application.Services.Receipts;

public sealed class ReceiptService : IReceiptService
{
    private readonly ISaleRepository _sales;
    private readonly IReceiptStorage _storage;
    private readonly CompanyOptions _company;

    public ReceiptService(ISaleRepository sales, IReceiptStorage storage, IOptions<CompanyOptions> company)
    {
        DocumentLibraries.EnsureConfigured();
        _sales = sales;
        _storage = storage;
        _company = company.Value;
    }

    /// <summary>Un archivo por venta: el numero es unico.</summary>
    public static string FileNameFor(string saleNumber) => $"{saleNumber}.pdf";

    public byte[] Generate(SaleResponse sale) => new ReceiptDocument(sale, _company).GeneratePdf();

    public async Task<string> SaveAsync(SaleResponse sale, CancellationToken cancellationToken = default)
    {
        var nombre = FileNameFor(sale.SaleNumber);
        await _storage.SaveAsync(nombre, Generate(sale), cancellationToken);
        return _storage.GetRelativePath(nombre);
    }

    public async Task<Result<ReceiptFile>> GetAsync(Guid saleId, CancellationToken cancellationToken = default)
    {
        var sale = await _sales.FindByIdAsync(saleId, cancellationToken);
        if (sale is null)
        {
            return Result.Failure<ReceiptFile>(Error.EntityNotFound("Venta", saleId));
        }

        var nombre = FileNameFor(sale.SaleNumber);
        var contenido = await _storage.ReadAsync(nombre, cancellationToken);
        if (contenido is null)
        {
            contenido = Generate(SaleMappings.ToResponse(sale));
            await _storage.SaveAsync(nombre, contenido, cancellationToken);
        }

        return Result.Success(new ReceiptFile(nombre, contenido));
    }
}
