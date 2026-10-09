using System.Net;
using System.Text;
using InfectionSolutions.Application.Common;
using InfectionSolutions.Application.Dtos.Sales;

namespace InfectionSolutions.Application.Services.Notifications;

public sealed record EmailContent(string Subject, string Html, string Text);

/// <summary>
/// Correos en HTML con estilos en linea (los clientes de correo ignoran las
/// hojas de estilo) y su version en texto plano. Mismo sistema visual del
/// panel: grafito, papel y el amarillo de bulto solo para la cifra clave.
/// </summary>
public static class EmailTemplates
{
    private const string Grafito = "#1F2427";
    private const string Acero = "#5E676D";
    private const string Papel = "#F6F6F3";
    private const string Linea = "#E3E4E0";
    private const string Bulto = "#F2B705";

    public static EmailContent Welcome(string fullName, CompanyOptions company)
    {
        var nombre = PrimerNombre(fullName);
        var asunto = $"Bienvenido a {company.Name}";

        var cuerpo = $"""
            <p style="margin:0 0 16px;font-size:16px;">Hola {Html(nombre)},</p>
            <p style="margin:0 0 16px;">Tu cuenta en el portal de clientes de {Html(company.Name)} quedó creada. Ya puedes
            consultar el catálogo, armar tu pedido y recibir el comprobante de cada compra en este correo.</p>
            <p style="margin:0;color:{Acero};font-size:13px;">Si no fuiste tú quien se registró, responde este mensaje y revisamos la cuenta.</p>
            """;

        var texto = $"""
            Hola {nombre},

            Tu cuenta en el portal de clientes de {company.Name} quedó creada. Ya puedes consultar el catálogo,
            armar tu pedido y recibir el comprobante de cada compra en este correo.

            Si no fuiste tú quien se registró, responde este mensaje y revisamos la cuenta.

            {Firma(company)}
            """;

        return new EmailContent(asunto, Plantilla(asunto, cuerpo, company), texto);
    }

    public static EmailContent PurchaseConfirmation(SaleResponse sale, CompanyOptions company)
    {
        var asunto = $"Comprobante de tu compra {sale.SaleNumber}";

        var filas = new StringBuilder();
        foreach (var detalle in sale.Details)
        {
            filas.Append($"""
                <tr>
                  <td style="padding:10px 0;border-bottom:1px solid {Linea};">{Html(detalle.ProductName)}<br>
                    <span style="color:{Acero};font-size:12px;">{detalle.Quantity} {Html(detalle.Unit.ToLowerInvariant())} × {DisplayFormats.Money(detalle.UnitPrice)}</span></td>
                  <td style="padding:10px 0;border-bottom:1px solid {Linea};text-align:right;white-space:nowrap;">{DisplayFormats.Money(detalle.LineTotal)}</td>
                </tr>
                """);
        }

        var cuerpo = $"""
            <p style="margin:0 0 16px;font-size:16px;">Hola {Html(PrimerNombre(sale.CustomerName))},</p>
            <p style="margin:0 0 20px;">Registramos tu compra. Adjuntamos el comprobante en PDF; guárdalo para reclamar el despacho.</p>
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="margin:0 0 8px;font-size:13px;color:{Acero};">
              <tr><td>Compra <strong style="color:{Grafito};">{Html(sale.SaleNumber)}</strong></td>
                  <td style="text-align:right;">{DisplayFormats.DateTime(sale.SaleDate)}</td></tr>
            </table>
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="font-size:14px;border-top:2px solid {Grafito};">
              {filas}
              <tr><td style="padding:12px 0 2px;color:{Acero};">Subtotal</td><td style="padding:12px 0 2px;text-align:right;">{DisplayFormats.Money(sale.Subtotal)}</td></tr>
              <tr><td style="padding:2px 0;color:{Acero};">IVA 19 %</td><td style="padding:2px 0;text-align:right;">{DisplayFormats.Money(sale.Tax)}</td></tr>
              <tr><td style="padding:12px 0 0;font-weight:700;font-size:16px;">Total</td>
                  <td style="padding:12px 0 0;text-align:right;font-weight:700;font-size:20px;"><span style="border-bottom:4px solid {Bulto};">{DisplayFormats.Money(sale.Total)}</span></td></tr>
            </table>
            """;

        var lineas = string.Join(Environment.NewLine, sale.Details.Select(d =>
            $"- {d.ProductName}: {d.Quantity} x {DisplayFormats.Money(d.UnitPrice)} = {DisplayFormats.Money(d.LineTotal)}"));

        var texto = $"""
            Hola {PrimerNombre(sale.CustomerName)},

            Registramos tu compra {sale.SaleNumber} del {DisplayFormats.DateTime(sale.SaleDate)}.
            Adjuntamos el comprobante en PDF.

            {lineas}

            Subtotal: {DisplayFormats.Money(sale.Subtotal)}
            IVA 19 %: {DisplayFormats.Money(sale.Tax)}
            Total: {DisplayFormats.Money(sale.Total)}

            {Firma(company)}
            """;

        return new EmailContent(asunto, Plantilla(asunto, cuerpo, company), texto);
    }

    private static string Plantilla(string titulo, string cuerpo, CompanyOptions company) => $"""
        <!doctype html>
        <html lang="es">
        <head><meta charset="utf-8"><meta name="viewport" content="width=device-width"><title>{Html(titulo)}</title></head>
        <body style="margin:0;padding:0;background:{Papel};">
          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:{Papel};padding:24px 12px;">
            <tr><td align="center">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:560px;background:#FFFFFF;border-radius:12px;overflow:hidden;font-family:Arial,Helvetica,sans-serif;color:{Grafito};line-height:1.5;">
                <tr><td style="background:{Grafito};padding:20px 28px;">
                  <span style="color:#FFFFFF;font-size:18px;font-weight:700;letter-spacing:.3px;">{Html(company.Name)}</span><br>
                  <span style="color:#B9BFC2;font-size:12px;">{Html(company.Tagline)}</span>
                </td></tr>
                <tr><td style="height:4px;background:{Bulto};font-size:0;line-height:0;">&nbsp;</td></tr>
                <tr><td style="padding:28px;font-size:14px;">{cuerpo}</td></tr>
                <tr><td style="padding:18px 28px;border-top:1px solid {Linea};font-size:12px;color:{Acero};">
                  {Html(company.Name)} &nbsp;|&nbsp; NIT {Html(company.TaxId)}<br>
                  {Html(company.Address)}<br>
                  {Html(company.Phone)} &nbsp;|&nbsp; {Html(company.Email)}
                </td></tr>
              </table>
            </td></tr>
          </table>
        </body>
        </html>
        """;

    private static string Firma(CompanyOptions company)
        => $"{company.Name} - NIT {company.TaxId}{Environment.NewLine}{company.Address}{Environment.NewLine}{company.Phone} | {company.Email}";

    private static string PrimerNombre(string nombreCompleto)
    {
        var partes = nombreCompleto.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return partes.Length == 0 ? nombreCompleto : partes[0];
    }

    private static string Html(string? valor) => WebUtility.HtmlEncode(valor ?? string.Empty);
}
