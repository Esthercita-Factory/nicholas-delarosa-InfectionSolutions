using System.Security.Claims;
using InfectionSolutions.Admin.Common;
using InfectionSolutions.Admin.ViewModels.Sales;
using InfectionSolutions.Application.Common;
using InfectionSolutions.Application.Dtos.Sales;
using InfectionSolutions.Application.Services.Customers;
using InfectionSolutions.Application.Services.Exports;
using InfectionSolutions.Application.Services.Products;
using InfectionSolutions.Application.Services.Receipts;
using InfectionSolutions.Application.Services.Sales;
using InfectionSolutions.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace InfectionSolutions.Admin.Controllers;

public class SalesController : Controller
{
    private readonly ISaleService _saleService;
    private readonly ICustomerService _customerService;
    private readonly IProductService _productService;
    private readonly IReceiptService _receiptService;
    private readonly IExportService _exportService;

    public SalesController(
        ISaleService saleService,
        ICustomerService customerService,
        IProductService productService,
        IReceiptService receiptService,
        IExportService exportService)
    {
        _saleService = saleService;
        _customerService = customerService;
        _productService = productService;
        _receiptService = receiptService;
        _exportService = exportService;
    }

    public async Task<IActionResult> Index(string? q, SaleStatus? status, int page = 1, CancellationToken cancellationToken = default)
    {
        var resultado = await _saleService.ListAsync(new SaleQuery { Q = q, Status = status, Page = page }, cancellationToken);

        return View(new SaleIndexViewModel
        {
            Result = resultado,
            Q = q,
            Status = status
        });
    }

    /// <summary>Exporta a Excel o PDF lo mismo que muestra el listado con sus filtros (todas las paginas).</summary>
    public async Task<IActionResult> Export(string? q, SaleStatus? status, ExportFormat format = ExportFormat.Excel, CancellationToken cancellationToken = default)
    {
        var archivo = await _exportService.ExportSalesAsync(new SaleQuery { Q = q, Status = status }, format, cancellationToken);
        return File(archivo.Content, archivo.ContentType, archivo.FileName);
    }

    /// <summary>
    /// Recibo PDF guardado en wwwroot/recibos. download=false lo abre en el
    /// navegador; si no existe (venta importada) se genera en el momento.
    /// </summary>
    public async Task<IActionResult> Receipt(Guid id, bool download = true, CancellationToken cancellationToken = default)
    {
        var resultado = await _receiptService.GetAsync(id, cancellationToken);
        if (resultado.IsFailure)
        {
            return NotFound();
        }

        var recibo = resultado.Value!;
        return download
            ? File(recibo.Content, ReceiptFile.ContentType, recibo.FileName)
            : File(recibo.Content, ReceiptFile.ContentType);
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await _saleService.GetByIdAsync(id, cancellationToken);
        return resultado.IsSuccess ? View(resultado.Value) : NotFound();
    }

    /// <summary>customerId permite abrir el formulario desde el detalle de un cliente.</summary>
    public async Task<IActionResult> Create(Guid? customerId, CancellationToken cancellationToken)
    {
        var modelo = new SaleFormViewModel
        {
            CustomerId = customerId,
            Lines = [new SaleLineViewModel()]
        };

        await CargarOpcionesAsync(modelo, cancellationToken);
        return View(modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SaleFormViewModel modelo, CancellationToken cancellationToken)
    {
        if (modelo.Lines.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Agrega al menos un producto a la venta.");
        }

        if (ModelState.IsValid)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var resultado = await _saleService.CreateAsync(modelo.ToRequest(), userId, cancellationToken);
            if (resultado.IsSuccess)
            {
                TempData[TempDataKeys.Mensaje] = $"La venta {resultado.Value!.SaleNumber} se registró por {resultado.Value.Total:C2} y su recibo PDF está listo para descargar.";
                return RedirectToAction(nameof(Details), new { id = resultado.Value.Id });
            }

            // Los errores de lineas van al resumen: no hay un campo unico al que asociarlos.
            var error = resultado.Error!;
            ModelState.AddModelError(error.Field == nameof(SaleRequest.CustomerId) ? nameof(modelo.CustomerId) : string.Empty, error.Message);
        }

        if (modelo.Lines.Count == 0)
        {
            modelo.Lines.Add(new SaleLineViewModel());
        }

        await CargarOpcionesAsync(modelo, cancellationToken);
        return View(modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await _saleService.CancelAsync(id, cancellationToken);
        if (resultado.IsFailure && resultado.Error!.Code == ErrorCode.NotFound)
        {
            return NotFound();
        }

        if (resultado.IsSuccess)
        {
            TempData[TempDataKeys.Mensaje] = "La venta se anuló, el stock de sus productos se devolvió al inventario y el recibo quedó marcado como ANULADA.";
        }
        else
        {
            TempData[TempDataKeys.Error] = resultado.Error!.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task CargarOpcionesAsync(SaleFormViewModel modelo, CancellationToken cancellationToken)
    {
        modelo.Customers = await _customerService.GetOptionsAsync(cancellationToken);
        modelo.Products = await _productService.GetOptionsAsync(cancellationToken);
    }
}
