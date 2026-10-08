namespace InfectionSolutions.Application.Common;

/// <summary>
/// Error esperado de un caso de uso. Field indica el campo del formulario al
/// que pertenece, cuando aplica, para mostrarlo junto al input.
/// </summary>
public sealed record Error(ErrorCode Code, string Message, string? Field = null)
{
    public static Error Validation(string message, string? field = null) => new(ErrorCode.Validation, message, field);

    public static Error EntityNotFound(string entity, Guid id) => new(ErrorCode.NotFound, $"{entity} con id {id} no existe.");

    public static Error Conflict(string message, string? field = null) => new(ErrorCode.Conflict, message, field);

    public static Error BusinessRule(string message, string? field = null) => new(ErrorCode.BusinessRule, message, field);
}
