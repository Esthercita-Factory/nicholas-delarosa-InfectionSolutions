using System.Globalization;
using InfectionSolutions.Application.Common;

namespace InfectionSolutions.Application.Services.BulkImport;

/// <summary>
/// Convierte los valores de una celda. Excel entrega numeros como double y
/// fechas como DateTime, pero un archivo armado a mano trae texto: "$32.500",
/// "1.250,50", "15/10/2026". Cada metodo devuelve un mensaje cuando no puede.
/// </summary>
public static class ImportValueParser
{
    private static readonly string[] FormatosDeFecha =
    [
        "dd/MM/yyyy", "d/M/yyyy", "dd/MM/yyyy HH:mm", "d/M/yyyy H:mm", "dd/MM/yyyy HH:mm:ss",
        "yyyy-MM-dd", "yyyy-MM-dd HH:mm", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm:ss",
        "dd-MM-yyyy", "yyyy/MM/dd"
    ];

    public static string? Text(object? value)
    {
        var texto = value switch
        {
            null => null,
            double d => d.ToString("0.##########", CultureInfo.InvariantCulture),
            DateTime fecha => fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture)
        };

        return string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
    }

    public static bool TryDecimal(object? value, out decimal result, out string? error)
    {
        result = 0;
        error = null;

        switch (value)
        {
            case double d:
                result = decimal.Round((decimal)d, 2, MidpointRounding.AwayFromZero);
                return true;
            case decimal m:
                result = decimal.Round(m, 2, MidpointRounding.AwayFromZero);
                return true;
            case int i:
                result = i;
                return true;
        }

        var texto = Text(value);
        if (texto is null)
        {
            error = "está vacío";
            return false;
        }

        var limpio = new string(texto.Where(c => char.IsDigit(c) || c is '.' or ',' or '-').ToArray());
        if (limpio.Length == 0 || !decimal.TryParse(NormalizarSeparadores(limpio), NumberStyles.Number, CultureInfo.InvariantCulture, out result))
        {
            error = $"'{texto}' no es un número válido";
            return false;
        }

        result = decimal.Round(result, 2, MidpointRounding.AwayFromZero);
        return true;
    }

    public static bool TryInteger(object? value, out int result, out string? error)
    {
        result = 0;
        if (!TryDecimal(value, out var numero, out error))
        {
            return false;
        }

        if (numero != decimal.Truncate(numero))
        {
            error = $"'{Text(value)}' debe ser un número entero";
            return false;
        }

        if (numero is > int.MaxValue or < int.MinValue)
        {
            error = $"'{Text(value)}' es demasiado grande";
            return false;
        }

        result = (int)numero;
        return true;
    }

    /// <summary>La fecha sin zona se interpreta como hora de Colombia.</summary>
    public static bool TryDate(object? value, out DateTimeOffset result, out string? error)
    {
        result = default;
        error = null;

        DateTime fecha;
        switch (value)
        {
            case DateTime dt:
                fecha = dt;
                break;
            case double serial when serial is > 0 and < 2958466:
                // Celda con formato general: Excel guarda las fechas como numero de serie.
                fecha = DateTime.FromOADate(serial);
                break;
            default:
                var texto = Text(value);
                if (texto is null)
                {
                    error = "está vacía";
                    return false;
                }

                if (!DateTime.TryParseExact(texto, FormatosDeFecha, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out fecha))
                {
                    error = $"'{texto}' no es una fecha válida (usa dd/mm/aaaa o aaaa-mm-dd)";
                    return false;
                }

                break;
        }

        result = new DateTimeOffset(DateTime.SpecifyKind(fecha, DateTimeKind.Unspecified), DisplayFormats.BusinessOffset).ToUniversalTime();
        return true;
    }

    /// <summary>
    /// Con punto y coma, el ultimo es el decimal ("1.250,50" o "1,250.50").
    /// Con un solo tipo: si se repite o le siguen exactamente 3 digitos es de
    /// miles ("32.500" = 32500, como se escribe en pesos); si no, es decimal.
    /// </summary>
    private static string NormalizarSeparadores(string numero)
    {
        var ultimoPunto = numero.LastIndexOf('.');
        var ultimaComa = numero.LastIndexOf(',');

        if (ultimoPunto >= 0 && ultimaComa >= 0)
        {
            var decimalSep = ultimoPunto > ultimaComa ? '.' : ',';
            var milesSep = decimalSep == '.' ? ',' : '.';
            return numero.Replace(milesSep.ToString(), string.Empty).Replace(decimalSep, '.');
        }

        var separador = ultimoPunto >= 0 ? '.' : ultimaComa >= 0 ? ',' : '\0';
        if (separador == '\0')
        {
            return numero;
        }

        var apariciones = numero.Count(c => c == separador);
        var digitosDespues = numero.Length - numero.LastIndexOf(separador) - 1;
        var esDeMiles = apariciones > 1 || digitosDespues == 3;

        return esDeMiles ? numero.Replace(separador.ToString(), string.Empty) : numero.Replace(separador, '.');
    }
}
