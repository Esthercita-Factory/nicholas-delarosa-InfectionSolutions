using System.Text.Json;
using InfectionSolutions.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace InfectionSolutions.Api.Common;

/// <summary>
/// Traduce los Result de Application a HTTP. Los errores salen como
/// ProblemDetails (RFC 9457); los de validacion con el campo en "errors",
/// igual que los que genera [ApiController] con las DataAnnotations.
/// </summary>
public static class ApiResults
{
    public static ActionResult<T> OkOrProblem<T>(this ControllerBase controller, Result<T> result)
        => result.IsSuccess ? controller.Ok(result.Value) : controller.ProblemFor(result.Error!);

    public static ActionResult NoContentOrProblem(this ControllerBase controller, Result result)
        => result.IsSuccess ? controller.NoContent() : controller.ProblemFor(result.Error!);

    public static ObjectResult ProblemFor(this ControllerBase controller, Error error)
    {
        var status = StatusFor(error.Code);

        // Validacion o duplicado con campo: el SPA lo pinta junto al input.
        var problema = error.Field is not null && error.Code is ErrorCode.Validation or ErrorCode.Conflict
            ? new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [JsonNamingPolicy.CamelCase.ConvertName(error.Field)] = [error.Message]
            })
            : new ProblemDetails();

        problema.Status = status;
        problema.Title = TitleFor(error.Code);
        problema.Detail = error.Message;
        problema.Instance = controller.HttpContext?.Request.Path.Value;

        return new ObjectResult(problema)
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" }
        };
    }

    public static int StatusFor(ErrorCode code) => code switch
    {
        ErrorCode.Validation => StatusCodes.Status400BadRequest,
        ErrorCode.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorCode.Forbidden => StatusCodes.Status403Forbidden,
        ErrorCode.NotFound => StatusCodes.Status404NotFound,
        ErrorCode.Conflict => StatusCodes.Status409Conflict,
        ErrorCode.BusinessRule => StatusCodes.Status422UnprocessableEntity,
        _ => StatusCodes.Status500InternalServerError
    };

    private static string TitleFor(ErrorCode code) => code switch
    {
        ErrorCode.Validation => "Datos inválidos",
        ErrorCode.Unauthorized => "No autenticado",
        ErrorCode.Forbidden => "Acceso denegado",
        ErrorCode.NotFound => "Recurso no encontrado",
        ErrorCode.Conflict => "Conflicto con los datos existentes",
        ErrorCode.BusinessRule => "Regla de negocio",
        _ => "Error inesperado"
    };
}
