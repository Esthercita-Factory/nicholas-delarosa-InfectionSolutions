namespace InfectionSolutions.Domain.Services;

public static class SaleNumberGenerator
{
    public const string Prefix = "V";

    /// <summary>Numero legible de la venta: V-20261007-0001.</summary>
    public static string Generate(DateTimeOffset date, int sequenceOfDay)
    {
        if (sequenceOfDay <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequenceOfDay), "El consecutivo debe ser mayor que cero.");
        }

        return $"{Prefix}-{date:yyyyMMdd}-{sequenceOfDay:D4}";
    }

    /// <summary>Prefijo de las ventas de un dia, para contar el consecutivo.</summary>
    public static string DayPrefix(DateTimeOffset date) => $"{Prefix}-{date:yyyyMMdd}-";
}
