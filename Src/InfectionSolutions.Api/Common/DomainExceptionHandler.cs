using InfectionSolutions.Domain.Errors;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace InfectionSolutions.Api.Common;

/// <summary>
/// Ultima red: una regla de negocio que escapa como excepcion (por ejemplo un
/// duplicado que gano la carrera contra la validacion del servicio) sale como
/// ProblemDetails; cualquier otra excepcion, como 500 sin detalles internos.
/// </summary>
public sealed class DomainExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetails;
    private readonly ILogger<DomainExceptionHandler> _logger;

    public DomainExceptionHandler(IProblemDetailsService problemDetails, ILogger<DomainExceptionHandler> logger)
    {
        _problemDetails = problemDetails;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            DuplicateEntityException => (StatusCodes.Status409Conflict, "Conflicto con los datos existentes", exception.Message),
            BusinessRuleViolationException => (StatusCodes.Status422UnprocessableEntity, "Regla de negocio", exception.Message),
            DomainException => (StatusCodes.Status400BadRequest, "Datos inválidos", exception.Message),
            _ => (StatusCodes.Status500InternalServerError, "Error inesperado", "Ocurrió un error al procesar la solicitud.")
        };

        if (status >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Error no controlado en {Metodo} {Ruta}", httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            _logger.LogWarning(exception, "Regla de negocio violada en {Metodo} {Ruta}", httpContext.Request.Method, httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = status;

        return await _problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = detail }
        });
    }
}
