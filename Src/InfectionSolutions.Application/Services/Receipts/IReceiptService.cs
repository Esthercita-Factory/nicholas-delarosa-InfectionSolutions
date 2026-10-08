using InfectionSolutions.Application.Common;
using InfectionSolutions.Application.Dtos.Sales;

namespace InfectionSolutions.Application.Services.Receipts;

public sealed record ReceiptFile(string FileName, byte[] Content)
{
    public const string ContentType = "application/pdf";
}

public interface IReceiptService
{
    /// <summary>Recibo en PDF: datos del cliente, numero y fecha, productos, subtotal, IVA y total.</summary>
    byte[] Generate(SaleResponse sale);

    /// <summary>Genera el recibo y lo guarda (reemplaza el anterior). Devuelve la ruta relativa.</summary>
    Task<string> SaveAsync(SaleResponse sale, CancellationToken cancellationToken = default);

    /// <summary>El recibo guardado; si no existe (p. ej. venta importada) lo genera y lo guarda.</summary>
    Task<Result<ReceiptFile>> GetAsync(Guid saleId, CancellationToken cancellationToken = default);
}
