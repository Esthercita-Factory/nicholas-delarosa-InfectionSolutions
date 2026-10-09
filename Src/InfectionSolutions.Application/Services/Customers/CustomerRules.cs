using InfectionSolutions.Application.Common;
using InfectionSolutions.Application.Dtos.Customers;

namespace InfectionSolutions.Application.Services.Customers;

/// <summary>
/// Reglas de los datos de un cliente. Las usan el CRUD del panel/API y el
/// registro del portal, para que un cliente valga lo mismo entre por donde entre.
/// </summary>
public static class CustomerRules
{
    public static Error? Validate(CustomerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Document))
        {
            return Error.Validation("El documento es obligatorio.", nameof(request.Document));
        }

        if (!FieldRules.IsValidDocument(FieldRules.NormalizeDocument(request.Document)))
        {
            return Error.Validation(
                $"El documento debe tener entre {FieldRules.DocumentMinLength} y {FieldRules.DocumentMaxLength} letras o números.",
                nameof(request.Document));
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            return Error.Validation("El nombre es obligatorio.", nameof(request.FullName));
        }

        if (request.Age is < InputParser.MinAge or > InputParser.MaxAge)
        {
            return Error.Validation($"La edad debe estar entre {InputParser.MinAge} y {InputParser.MaxAge} años.", nameof(request.Age));
        }

        if (!FieldRules.IsValidEmail(request.Email))
        {
            return Error.Validation("El correo no tiene un formato válido.", nameof(request.Email));
        }

        if (!FieldRules.IsValidPhone(request.Phone))
        {
            return Error.Validation("El teléfono es obligatorio y solo admite números, espacios, guiones, paréntesis y + al inicio.", nameof(request.Phone));
        }

        return null;
    }
}
