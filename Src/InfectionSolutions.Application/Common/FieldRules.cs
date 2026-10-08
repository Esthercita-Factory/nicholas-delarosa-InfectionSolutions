using System.Net.Mail;
using System.Text.RegularExpressions;

namespace InfectionSolutions.Application.Common;

/// <summary>
/// Reglas de formato y normalizacion compartidas entre los formularios, la
/// carga masiva y (en la S3) la API: un dato se valida igual entre por donde entre.
/// Las longitudes coinciden con las columnas de la base.
/// </summary>
public static partial class FieldRules
{
    public const int SkuMaxLength = 30;
    public const int ProductNameMaxLength = 120;
    public const int CategoryMaxLength = 80;
    public const int UnitMaxLength = 30;
    public const int DescriptionMaxLength = 1000;

    public const int DocumentMinLength = 5;
    public const int DocumentMaxLength = 20;
    public const int FullNameMaxLength = 120;
    public const int EmailMaxLength = 120;
    public const int PhoneMaxLength = 20;
    public const int AddressMaxLength = 150;

    public const int SaleNumberMaxLength = 30;

    public static string NormalizeSku(string sku) => sku.Trim().ToUpperInvariant();

    public static bool IsValidSku(string sku) => sku.Length <= SkuMaxLength && SkuPattern().IsMatch(sku);

    /// <summary>Quita puntos, espacios y guiones: "1.020.458.796" y "1020458796" son el mismo documento.</summary>
    public static string NormalizeDocument(string document)
        => new string(document.Where(c => c is not ('.' or ' ' or '-' or ',')).ToArray()).ToUpperInvariant();

    public static bool IsValidDocument(string document)
        => document.Length is >= DocumentMinLength and <= DocumentMaxLength && document.All(char.IsLetterOrDigit);

    /// <summary>email es unique: se guarda en minusculas para no duplicar por mayusculas.</summary>
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > EmailMaxLength)
        {
            return false;
        }

        try
        {
            var direccion = new MailAddress(email.Trim());
            return direccion.Address == email.Trim() && direccion.Host.Contains('.');
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>
    /// Llave para comparar textos en memoria sin mayusculas ni tildes:
    /// "Mampostería" y "mamposteria" dan la misma llave.
    /// </summary>
    public static string ComparisonKey(string value)
    {
        var descompuesto = value.Trim().ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
        var limpio = descompuesto.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark);
        return new string(limpio.ToArray()).Normalize(System.Text.NormalizationForm.FormC);
    }

    public static bool IsValidPhone(string? phone)
        => !string.IsNullOrWhiteSpace(phone) && PhonePattern().IsMatch(phone.Trim());

    [GeneratedRegex(@"^[A-Za-z0-9\-_.]+$")]
    private static partial Regex SkuPattern();

    [GeneratedRegex(@"^\+?[0-9\s\-()]{7,20}$")]
    private static partial Regex PhonePattern();
}
