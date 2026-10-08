namespace InfectionSolutions.Application.Abstractions;

/// <summary>
/// Donde quedan guardados los recibos PDF. El panel los guarda en
/// wwwroot/recibos; otra implementacion podria usar un bucket.
/// </summary>
public interface IReceiptStorage
{
    Task SaveAsync(string fileName, byte[] content, CancellationToken cancellationToken = default);

    /// <summary>null si el archivo no existe.</summary>
    Task<byte[]?> ReadAsync(string fileName, CancellationToken cancellationToken = default);

    /// <summary>Ruta relativa con la que se identifica el archivo (p. ej. recibos/V-20261007-0001.pdf).</summary>
    string GetRelativePath(string fileName);
}
