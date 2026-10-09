using InfectionSolutions.Application.Common;
using InfectionSolutions.Application.Services.Auth;
using InfectionSolutions.Application.Services.BulkImport;
using InfectionSolutions.Application.Services.Customers;
using InfectionSolutions.Application.Services.Dashboard;
using InfectionSolutions.Application.Services.Exports;
using InfectionSolutions.Application.Services.Notifications;
using InfectionSolutions.Application.Services.Portal;
using InfectionSolutions.Application.Services.Products;
using InfectionSolutions.Application.Services.Receipts;
using InfectionSolutions.Application.Services.Sales;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InfectionSolutions.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        DocumentLibraries.EnsureConfigured();

        services.Configure<CompanyOptions>(configuration.GetSection(CompanyOptions.SectionName));

        // Perfiles entidad -> DTO de Mappings/. AutoMapper 15+ pide licencia (gratis
        // para uso no comercial): sin clave funciona igual y solo deja un aviso en el log.
        services.AddAutoMapper(
            options => options.LicenseKey = AutoMapperLicenseKey(configuration),
            typeof(ApplicationServiceCollectionExtensions).Assembly);

        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<ISaleService, SaleService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IReceiptService, ReceiptService>();
        services.AddScoped<IExportService, ExportService>();
        services.AddScoped<IBulkImportService, BulkImportService>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICustomerNotifier, CustomerNotifier>();
        services.AddScoped<ICustomerPortalService, CustomerPortalService>();

        return services;
    }

    /// <summary>Vacia = null: con "" AutoMapper intenta validarla y registra un error critico.</summary>
    public static string? AutoMapperLicenseKey(IConfiguration configuration)
        => string.IsNullOrWhiteSpace(configuration["AutoMapper:LicenseKey"]) ? null : configuration["AutoMapper:LicenseKey"];
}
