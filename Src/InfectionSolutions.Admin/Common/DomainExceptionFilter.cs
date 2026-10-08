using InfectionSolutions.Domain.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace InfectionSolutions.Admin.Common;

/// <summary>
/// Ultima red para errores de negocio que escapan del servicio (por ejemplo
/// un duplicado que gano la carrera contra la validacion): en lugar de la
/// pagina de error se vuelve al listado con un mensaje amigable.
/// </summary>
public sealed class DomainExceptionFilter : IExceptionFilter
{
    private readonly ITempDataDictionaryFactory _tempDataFactory;
    private readonly ILogger<DomainExceptionFilter> _logger;

    public DomainExceptionFilter(ITempDataDictionaryFactory tempDataFactory, ILogger<DomainExceptionFilter> logger)
    {
        _tempDataFactory = tempDataFactory;
        _logger = logger;
    }

    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not DomainException exception)
        {
            return;
        }

        _logger.LogWarning(exception, "Regla de negocio violada en {Ruta}", context.HttpContext.Request.Path);

        var tempData = _tempDataFactory.GetTempData(context.HttpContext);
        tempData[TempDataKeys.Error] = exception.Message;

        context.Result = new RedirectToActionResult("Index", null, null);
        context.ExceptionHandled = true;
    }
}
