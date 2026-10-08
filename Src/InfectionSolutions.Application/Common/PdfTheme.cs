using System.Globalization;
using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace InfectionSolutions.Application.Common;

/// <summary>
/// Identidad de los documentos, la misma del panel: gris cemento, grafito,
/// acero y el amarillo de los bultos como unica marca de enfasis. Archivo
/// condensada para titulos y cifras, Archivo normal para el texto.
/// </summary>
internal static class PdfTheme
{
    public const string TextFont = "Archivo";
    public const string CondensedFont = "Archivo Condensed";

    public const string Grafito = "#1F2427";
    public const string Acero = "#7D868C";
    public const string AceroClaro = "#B9BEC1";
    public const string Cemento = "#E6E7E3";
    public const string Papel = "#F6F6F3";
    public const string Linea = "#D3D5D1";
    public const string Bulto = "#F2B705";
    public const string Blanco = "#FFFFFF";
    public const string Rojo = "#B3261E";
    public const string Verde = "#2F6B4A";

    public static TextStyle Base => TextStyle.Default.FontFamily(TextFont).FontSize(9).FontColor(Grafito).LineHeight(1.25f);

    /// <summary>
    /// La cinta metrica: marcas cada 5 unidades y una mas larga cada 50. Es el
    /// unico adorno del sistema y separa el encabezado del contenido.
    /// </summary>
    public static void Ruler(IContainer container, string color = Grafito, float height = 7)
    {
        container.Height(height).Svg(size =>
        {
            var ancho = Math.Max(1, (int)size.Width);
            var alto = size.Height;
            var svg = new StringBuilder();
            svg.Append(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{ancho}\" height=\"{alto}\" viewBox=\"0 0 {ancho} {alto}\">");
            svg.Append(CultureInfo.InvariantCulture, $"<line x1=\"0\" y1=\"0.4\" x2=\"{ancho}\" y2=\"0.4\" stroke=\"{color}\" stroke-width=\"0.8\"/>");
            for (var x = 0; x <= ancho; x += 5)
            {
                var largo = x % 50 == 0 ? alto : x % 25 == 0 ? alto * 0.7f : alto * 0.4f;
                svg.Append(CultureInfo.InvariantCulture, $"<line x1=\"{x + 0.4f}\" y1=\"0\" x2=\"{x + 0.4f}\" y2=\"{largo}\" stroke=\"{color}\" stroke-width=\"0.6\"/>");
            }

            svg.Append("</svg>");
            return svg.ToString();
        });
    }
}
