using InfectionSolutions.Application.Abstractions;

namespace InfectionSolutions.Infrastructure.Storage;

/// <summary>Guarda los recibos como archivos en una carpeta (en el panel: wwwroot/recibos).</summary>
public sealed class FileSystemReceiptStorage : IReceiptStorage
{
    private readonly string _directory;
    private readonly string _relativeFolder;

    public FileSystemReceiptStorage(string directory, string relativeFolder = "recibos")
    {
        _directory = directory;
        _relativeFolder = relativeFolder;
        Directory.CreateDirectory(_directory);
    }

    public Task SaveAsync(string fileName, byte[] content, CancellationToken cancellationToken = default)
        => File.WriteAllBytesAsync(RutaSegura(fileName), content, cancellationToken);

    public async Task<byte[]?> ReadAsync(string fileName, CancellationToken cancellationToken = default)
    {
        var ruta = RutaSegura(fileName);
        return File.Exists(ruta) ? await File.ReadAllBytesAsync(ruta, cancellationToken) : null;
    }

    public string GetRelativePath(string fileName) => $"{_relativeFolder}/{Path.GetFileName(fileName)}";

    /// <summary>Solo el nombre del archivo: un "../" no puede salirse de la carpeta.</summary>
    private string RutaSegura(string fileName)
    {
        var nombre = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw new ArgumentException("Nombre de archivo inválido.", nameof(fileName));
        }

        return Path.Combine(_directory, nombre);
    }
}
