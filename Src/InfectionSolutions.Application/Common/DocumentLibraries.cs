using OfficeOpenXml;
using QuestPDF.Drawing;

namespace InfectionSolutions.Application.Common;

/// <summary>
/// EPPlus y QuestPDF exigen declarar la licencia antes de usarlos. Proyecto
/// de estudio: licencia no comercial de EPPlus y Community de QuestPDF.
/// Tambien registra la tipografia Archivo incrustada para los PDF.
/// </summary>
public static class DocumentLibraries
{
    private static readonly Lazy<bool> Configurado = new(() =>
    {
        ExcelPackage.License.SetNonCommercialOrganization("InfectionSolutions");
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        // Sin fuentes del sistema: el PDF sale igual en Windows y en el contenedor Linux.
        QuestPDF.Settings.UseSystemFonts = false;
        RegistrarFuentes();
        return true;
    });

    public static void EnsureConfigured() => _ = Configurado.Value;

    private static void RegistrarFuentes()
    {
        var ensamblado = typeof(DocumentLibraries).Assembly;
        foreach (var recurso in ensamblado.GetManifestResourceNames().Where(n => n.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)))
        {
            // La familia ("Archivo", "Archivo Condensed") y el peso salen del propio archivo.
            using var stream = ensamblado.GetManifestResourceStream(recurso)!;
            FontManager.RegisterFontFromStream(stream);
        }
    }
}
