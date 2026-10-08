using InfectionSolutions.Application.Common;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace InfectionSolutions.Admin.Common;

public static class ModelStateExtensions
{
    /// <summary>
    /// Pone el error del servicio junto al campo que lo causo; si no trae
    /// campo, va al resumen del formulario.
    /// </summary>
    public static void AddResultError(this ModelStateDictionary modelState, Error error)
    {
        modelState.AddModelError(error.Field ?? string.Empty, error.Message);
    }
}
