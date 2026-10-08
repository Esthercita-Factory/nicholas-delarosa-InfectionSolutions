using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using InfectionSolutions.Admin.ViewModels.Imports;
using InfectionSolutions.Application.Services.BulkImport;
using InfectionSolutions.Application.Services.Exports;
using Microsoft.AspNetCore.Mvc;

namespace InfectionSolutions.Admin.Controllers;

public partial class ImportsController : Controller
{
    private readonly IBulkImportService _importService;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<ImportsController> _logger;

    public ImportsController(IBulkImportService importService, IWebHostEnvironment environment, ILogger<ImportsController> logger)
    {
        _importService = importService;
        _environment = environment;
        _logger = logger;
    }

    /// <summary>Los logs quedan fuera de wwwroot: solo se descargan por la accion Log.</summary>
    private string CarpetaDeLogs => Path.Combine(_environment.ContentRootPath, "App_Data", "import-logs");

    public IActionResult Index()
    {
        return View(new ImportUploadViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(ImportUploadViewModel.MaxFileBytes + 64 * 1024)]
    public async Task<IActionResult> Index(ImportUploadViewModel modelo, CancellationToken cancellationToken)
    {
        if (modelo.File is { } archivo)
        {
            if (!Path.GetExtension(archivo.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(modelo.File), "Solo se aceptan archivos .xlsx (Excel 2007 o superior).");
            }
            else if (archivo.Length == 0)
            {
                ModelState.AddModelError(nameof(modelo.File), "El archivo está vacío.");
            }
            else if (archivo.Length > ImportUploadViewModel.MaxFileBytes)
            {
                ModelState.AddModelError(nameof(modelo.File), "El archivo supera el máximo de 10 MB.");
            }
        }

        if (!ModelState.IsValid)
        {
            return View(modelo);
        }

        var nombre = Path.GetFileName(modelo.File!.FileName);
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        await using var stream = modelo.File.OpenReadStream();
        var resultado = await _importService.ImportAsync(stream, nombre, userId, cancellationToken);

        _logger.LogInformation(
            "Importación de {Archivo}: {Creados} creados/actualizados, {Errores} errores",
            nombre, resultado.TotalChanges, resultado.ErrorCount);

        return View("Result", new ImportResultViewModel
        {
            Result = resultado,
            LogFileName = await GuardarLogAsync(resultado, cancellationToken)
        });
    }

    public IActionResult Template()
    {
        return File(_importService.BuildTemplate(), ExportFile.ExcelContentType, "plantilla-carga-masiva.xlsx");
    }

    public IActionResult Log(string id)
    {
        // Solo nombres generados por GuardarLogAsync: nada de rutas.
        if (!NombreDeLog().IsMatch(id))
        {
            return NotFound();
        }

        var ruta = Path.Combine(CarpetaDeLogs, id);
        return System.IO.File.Exists(ruta)
            ? PhysicalFile(ruta, "text/csv; charset=utf-8", id)
            : NotFound();
    }

    private async Task<string?> GuardarLogAsync(ImportResult resultado, CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(CarpetaDeLogs);
            var nombre = $"import-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..8]}.csv";

            // Con BOM: Excel en español abre el UTF-8 sin dañar las tildes.
            await System.IO.File.WriteAllTextAsync(Path.Combine(CarpetaDeLogs, nombre), resultado.ToCsv(), new UTF8Encoding(true), cancellationToken);
            return nombre;
        }
        catch (IOException exception)
        {
            _logger.LogError(exception, "No se pudo guardar el log de la importación");
            return null;
        }
    }

    [GeneratedRegex(@"^import-\d{8}-\d{6}-[a-f0-9]{8}\.csv$")]
    private static partial Regex NombreDeLog();
}
