using System.ComponentModel.DataAnnotations;
using InfectionSolutions.Application.Services.BulkImport;

namespace InfectionSolutions.Admin.ViewModels.Imports;

public class ImportUploadViewModel
{
    public const long MaxFileBytes = 10 * 1024 * 1024;

    [Display(Name = "Archivo Excel (.xlsx)")]
    [Required(ErrorMessage = "Selecciona un archivo.")]
    public IFormFile? File { get; set; }
}

public class ImportResultViewModel
{
    public required ImportResult Result { get; init; }

    /// <summary>Nombre del CSV con el log, para el enlace de descarga.</summary>
    public string? LogFileName { get; init; }
}
