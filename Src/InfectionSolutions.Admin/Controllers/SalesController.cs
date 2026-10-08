using System.Security.Claims;
using InfectionSolutions.Admin.Common;
using InfectionSolutions.Admin.ViewModels.Sales;
using InfectionSolutions.Application.Common;
using InfectionSolutions.Application.Dtos.Sales;
using InfectionSolutions.Application.Services.Customers;
using InfectionSolutions.Application.Services.Products;
using InfectionSolutions.Application.Services.Sales;
using InfectionSolutions.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace InfectionSolutions.Admin.Controllers;

public class SalesController : Controller
{
    private readonly ISaleService _saleService;
    private readonly ICustomerService _customerService;
    private readonly IProductService _productService;

    public SalesController(ISaleService saleService, ICustomerService customerService, IProductService productService)
    {
        _saleService = saleService;
        _customerService = customerService;
        _productService = productService;
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
                TempData[TempDataKeys.Mensaje] = $"La venta {resultado.Value!.SaleNumber} se registró por {resultado.Value.Total:C2}.";
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
            TempData[TempDataKeys.Mensaje] = "La venta se anuló y el stock de sus productos se devolvió al inventario.";
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
