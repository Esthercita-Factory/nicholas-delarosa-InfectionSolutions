using InfectionSolutions.Admin.Common;
using InfectionSolutions.Admin.ViewModels.Products;
using InfectionSolutions.Application.Common;
using InfectionSolutions.Application.Dtos.Products;
using InfectionSolutions.Application.Services.Exports;
using InfectionSolutions.Application.Services.Products;
using Microsoft.AspNetCore.Mvc;

namespace InfectionSolutions.Admin.Controllers;

public class ProductsController : Controller
{
    private readonly IProductService _productService;
    private readonly IExportService _exportService;

    public ProductsController(IProductService productService, IExportService exportService)
    {
        _productService = productService;
        _exportService = exportService;
    }

    public async Task<IActionResult> Index(string? q, string? category, bool? active = null, int page = 1, CancellationToken cancellationToken = default)
    {
        var resultado = await _productService.ListAsync(new ProductQuery
        {
            Q = q,
            Category = category,
            Active = active,
            Page = page
        }, cancellationToken);

        return View(new ProductIndexViewModel
        {
            Result = resultado,
            Q = q,
            Category = category,
            Active = active,
            Categories = await _productService.GetCategoriesAsync(cancellationToken)
        });
    }

    /// <summary>Exporta a Excel o PDF lo mismo que muestra el listado con sus filtros (todas las paginas).</summary>
    public async Task<IActionResult> Export(string? q, string? category, bool? active = null, ExportFormat format = ExportFormat.Excel, CancellationToken cancellationToken = default)
    {
        var archivo = await _exportService.ExportProductsAsync(new ProductQuery { Q = q, Category = category, Active = active }, format, cancellationToken);
        return File(archivo.Content, archivo.ContentType, archivo.FileName);
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await _productService.GetByIdAsync(id, cancellationToken);
        return resultado.IsSuccess ? View(resultado.Value) : NotFound();
    }

    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var modelo = new ProductFormViewModel();
        await CargarCategoriasAsync(modelo, cancellationToken);
        return View(modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProductFormViewModel modelo, CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            var resultado = await _productService.CreateAsync(modelo.ToRequest(), cancellationToken);
            if (resultado.IsSuccess)
            {
                TempData[TempDataKeys.Mensaje] = $"El producto {resultado.Value!.Name} se creó correctamente.";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddResultError(resultado.Error!);
        }

        await CargarCategoriasAsync(modelo, cancellationToken);
        return View(modelo);
    }

    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await _productService.GetByIdAsync(id, cancellationToken);
        if (resultado.IsFailure)
        {
            return NotFound();
        }

        var modelo = resultado.Value!.ToFormViewModel();
        await CargarCategoriasAsync(modelo, cancellationToken);
        return View(modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, ProductFormViewModel modelo, CancellationToken cancellationToken)
    {
        if (id != modelo.Id)
        {
            return BadRequest();
        }

        if (ModelState.IsValid)
        {
            var resultado = await _productService.UpdateAsync(id, modelo.ToRequest(), cancellationToken);
            if (resultado.IsSuccess)
            {
                TempData[TempDataKeys.Mensaje] = $"El producto {resultado.Value!.Name} se actualizó correctamente.";
                return RedirectToAction(nameof(Index));
            }

            if (resultado.Error!.Code == ErrorCode.NotFound)
            {
                return NotFound();
            }

            ModelState.AddResultError(resultado.Error);
        }

        await CargarCategoriasAsync(modelo, cancellationToken);
        return View(modelo);
    }

    /// <summary>Pantalla de confirmacion del borrado.</summary>
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await _productService.GetByIdAsync(id, cancellationToken);
        return resultado.IsSuccess ? View(resultado.Value) : NotFound();
    }

    [HttpPost]
    [ActionName(nameof(Delete))]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await _productService.DeleteAsync(id, cancellationToken);
        if (resultado.IsSuccess)
        {
            TempData[TempDataKeys.Mensaje] = "El producto se eliminó correctamente.";
            return RedirectToAction(nameof(Index));
        }

        if (resultado.Error!.Code == ErrorCode.NotFound)
        {
            return NotFound();
        }

        TempData[TempDataKeys.Error] = resultado.Error.Message;
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task CargarCategoriasAsync(ProductFormViewModel modelo, CancellationToken cancellationToken)
    {
        modelo.Categories = await _productService.GetCategoriesAsync(cancellationToken);
    }
}
