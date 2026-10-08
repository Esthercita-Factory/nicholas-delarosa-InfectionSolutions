using InfectionSolutions.Application.Common;
using InfectionSolutions.Application.Services.BulkImport;
using InfectionSolutions.Application.Services.Customers;
using InfectionSolutions.Application.Services.Dashboard;
using InfectionSolutions.Application.Services.Exports;
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

        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<ISaleService, SaleService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IReceiptService, ReceiptService>();
        services.AddScoped<IExportService, ExportService>();
        services.AddScoped<IBulkImportService, BulkImportService>();

        return services;
    }
}
