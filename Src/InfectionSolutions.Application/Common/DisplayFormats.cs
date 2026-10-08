using System.Globalization;

namespace InfectionSolutions.Application.Common;

/// <summary>
/// Formato de dinero y fechas para documentos generados (PDF, Excel). Igual
/// que el panel: es-CO con punto decimal, y la hora de Colombia aunque el
/// servidor (o el contenedor) este en UTC.
/// </summary>
public static class DisplayFormats
{
    /// <summary>Colombia no tiene horario de verano: UTC-5 todo el año.</summary>
    public static readonly TimeSpan BusinessOffset = TimeSpan.FromHours(-5);

    public static readonly CultureInfo Culture = CrearCultura();

    public static string Money(decimal value) => value.ToString("C2", Culture);

    public static DateTimeOffset ToBusinessTime(DateTimeOffset value) => value.ToOffset(BusinessOffset);

    public static string DateTime(DateTimeOffset value) => ToBusinessTime(value).ToString("dd/MM/yyyy HH:mm", Culture);

    public static string Date(DateTimeOffset value) => ToBusinessTime(value).ToString("dd/MM/yyyy", Culture);

    private static CultureInfo CrearCultura()
    {
        var cultura = (CultureInfo)CultureInfo.GetCultureInfo("es-CO").Clone();
        cultura.NumberFormat.NumberDecimalSeparator = ".";
        cultura.NumberFormat.NumberGroupSeparator = ",";
        cultura.NumberFormat.CurrencyDecimalSeparator = ".";
        cultura.NumberFormat.CurrencyGroupSeparator = ",";
        return CultureInfo.ReadOnly(cultura);
    }
}
