using InfectionSolutions.Admin.Common;
using InfectionSolutions.Admin.ViewModels.Customers;
using InfectionSolutions.Application.Common;
using InfectionSolutions.Application.Dtos.Customers;
using InfectionSolutions.Application.Services.Customers;
using Microsoft.AspNetCore.Mvc;

namespace InfectionSolutions.Admin.Controllers;

public class CustomersController : Controller
{
    private readonly ICustomerService _customerService;

    public CustomersController(ICustomerService customerService)
    {
        _customerService = customerService;
    }

    public async Task<IActionResult> Index(string? q, bool onlyActive = false, int page = 1, CancellationToken cancellationToken = default)
    {
        var resultado = await _customerService.ListAsync(new CustomerQuery
        {
            Q = q,
            OnlyActive = onlyActive,
            Page = page
        }, cancellationToken);

        return View(new CustomerIndexViewModel
        {
            Result = resultado,
            Q = q,
            OnlyActive = onlyActive
        });
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await _customerService.GetByIdAsync(id, cancellationToken);
        return resultado.IsSuccess ? View(resultado.Value) : NotFound();
    }

    public IActionResult Create()
    {
        return View(new CustomerFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CustomerFormViewModel modelo, CancellationToken cancellationToken)
    {
        var edad = ConvertirEdad(modelo);

        if (ModelState.IsValid)
        {
            var resultado = await _customerService.CreateAsync(modelo.ToRequest(edad), cancellationToken);
            if (resultado.IsSuccess)
            {
                TempData[TempDataKeys.Mensaje] = $"El cliente {resultado.Value!.FullName} se creó correctamente.";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddResultError(resultado.Error!);
        }

        return View(modelo);
    }

    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await _customerService.GetByIdAsync(id, cancellationToken);
        return resultado.IsSuccess ? View(resultado.Value!.ToFormViewModel()) : NotFound();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, CustomerFormViewModel modelo, CancellationToken cancellationToken)
    {
        if (id != modelo.Id)
        {
            return BadRequest();
        }

        var edad = ConvertirEdad(modelo);

        if (ModelState.IsValid)
        {
            var resultado = await _customerService.UpdateAsync(id, modelo.ToRequest(edad), cancellationToken);
            if (resultado.IsSuccess)
            {
                TempData[TempDataKeys.Mensaje] = $"El cliente {resultado.Value!.FullName} se actualizó correctamente.";
                return RedirectToAction(nameof(Index));
            }

            if (resultado.Error!.Code == ErrorCode.NotFound)
            {
                return NotFound();
            }

            ModelState.AddResultError(resultado.Error);
        }

        return View(modelo);
    }

    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await _customerService.GetByIdAsync(id, cancellationToken);
        return resultado.IsSuccess ? View(resultado.Value) : NotFound();
    }

    [HttpPost]
    [ActionName(nameof(Delete))]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await _customerService.DeleteAsync(id, cancellationToken);
        if (resultado.IsSuccess)
        {
            TempData[TempDataKeys.Mensaje] = "El cliente se eliminó correctamente.";
            return RedirectToAction(nameof(Index));
        }

        if (resultado.Error!.Code == ErrorCode.NotFound)
        {
            return NotFound();
        }

        TempData[TempDataKeys.Error] = resultado.Error.Message;
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// La edad llega como texto: InputParser hace el int.Parse dentro de un
    /// try-catch y devuelve un mensaje amigable si no es un entero valido.
    /// </summary>
    private int ConvertirEdad(CustomerFormViewModel modelo)
    {
        // Si falta, [Required] ya dejo su mensaje: no se duplica.
        if (string.IsNullOrWhiteSpace(modelo.Age))
        {
            return 0;
        }

        var edad = InputParser.ParseAge(modelo.Age, nameof(modelo.Age));
        if (edad.IsFailure)
        {
            ModelState.AddResultError(edad.Error!);
            return 0;
        }

        return edad.Value;
    }
}
