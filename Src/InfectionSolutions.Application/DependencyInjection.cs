using InfectionSolutions.Application.Services.Customers;
using InfectionSolutions.Application.Services.Dashboard;
using InfectionSolutions.Application.Services.Products;
using InfectionSolutions.Application.Services.Sales;
using Microsoft.Extensions.DependencyInjection;

namespace InfectionSolutions.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<ISaleService, SaleService>();
        services.AddScoped<IDashboardService, DashboardService>();

        return services;
    }
}
