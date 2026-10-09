using InfectionSolutions.Application.Common;
using InfectionSolutions.Application.Dtos.Sales;
using InfectionSolutions.Application.Services.Notifications;
using InfectionSolutions.Application.Services.Receipts;
using InfectionSolutions.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace InfectionSolutions.Tests.Application;

public class CustomerNotifierTests
{
    private readonly IEmailSender _email = Substitute.For<IEmailSender>();
    private readonly IReceiptService _receipts = Substitute.For<IReceiptService>();
    private readonly CustomerNotifier _notifier;

    public CustomerNotifierTests()
    {
        _notifier = new CustomerNotifier(_email, _receipts, Options.Create(new CompanyOptions()), NullLogger<CustomerNotifier>.Instance);
    }

    private static SaleResponse Venta() => new(
        Guid.NewGuid(), "V-20261008-0003", new DateTimeOffset(2026, 10, 8, 15, 0, 0, TimeSpan.Zero), SaleStatus.Completed, Guid.NewGuid(),
        "Laura Mendoza", "1020", "laura@correo.com", "300", null, 64_000m, 12_160m, 76_160m, null, null,
        [new SaleDetailResponse(Guid.NewGuid(), "CEM-50", "Cemento <i>gris</i>", "Bulto", 2, 32_000m, 64_000m)]);

    [Fact]
    public async Task ConfirmacionDeCompra_AdjuntaElReciboPdf()
    {
        var venta = Venta();
        _receipts.GetAsync(venta.Id, Arg.Any<CancellationToken>())
            .Returns(Result.Success(new ReceiptFile("V-20261008-0003.pdf", [1, 2, 3])));

        var enviado = await _notifier.SendPurchaseConfirmationAsync(venta);

        Assert.True(enviado);
        await _email.Received(1).SendAsync(Arg.Is<EmailMessage>(m =>
            m.ToAddress == "laura@correo.com"
            && m.Subject.Contains("V-20261008-0003")
            && m.HtmlBody.Contains("$ 76,160.00")
            && m.Attachments!.Single().FileName == "V-20261008-0003.pdf"
            && m.Attachments!.Single().ContentType == "application/pdf"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SiElServidorDeCorreoFalla_DevuelveFalseSinLanzar()
    {
        var venta = Venta();
        _receipts.GetAsync(venta.Id, Arg.Any<CancellationToken>())
            .Returns(Result.Success(new ReceiptFile("V.pdf", [1])));
        _email.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new IOException("SMTP caído"));

        Assert.False(await _notifier.SendPurchaseConfirmationAsync(venta));
    }

    [Fact]
    public void Plantilla_EscapaElHtmlDeLosDatos()
    {
        var correo = EmailTemplates.PurchaseConfirmation(Venta(), new CompanyOptions());

        Assert.DoesNotContain("<i>gris</i>", correo.Html);
        Assert.Contains("Cemento &lt;i&gt;gris&lt;/i&gt;", correo.Html);
        Assert.Contains("Hola Laura,", correo.Html);
    }
}
