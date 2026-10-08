namespace InfectionSolutions.Application.Common;

/// <summary>
/// Conversion de texto ingresado por el usuario a numeros, con mensajes
/// amigables en lugar de dejar escapar FormatException/OverflowException.
/// </summary>
public static class InputParser
{
    public const int MinAge = 18;
    public const int MaxAge = 120;

    public static Result<int> ParseAge(string? input, string field = "Age")
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Result.Failure<int>(Error.Validation("La edad es obligatoria.", field));
        }

        int edad;
        try
        {
            edad = int.Parse(input.Trim());
        }
        catch (FormatException)
        {
            return Result.Failure<int>(Error.Validation("La edad debe ser un número entero, sin letras ni decimales.", field));
        }
        catch (OverflowException)
        {
            return Result.Failure<int>(Error.Validation("La edad ingresada es demasiado grande.", field));
        }

        if (edad < MinAge || edad > MaxAge)
        {
            return Result.Failure<int>(Error.Validation($"La edad debe estar entre {MinAge} y {MaxAge} años.", field));
        }

        return Result.Success(edad);
    }
}
