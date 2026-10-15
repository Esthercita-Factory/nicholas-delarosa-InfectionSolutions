using InfectionSolutions.Application.Abstractions;
using InfectionSolutions.Infrastructure.Email;
using InfectionSolutions.Infrastructure.Identity;
using InfectionSolutions.Infrastructure.Persistence;
using InfectionSolutions.Infrastructure.Repositories;
using InfectionSolutions.Infrastructure.Security;
using InfectionSolutions.Infrastructure.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InfectionSolutions.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Persistencia, repositorios e Identity (usuarios y roles). Cada host
    /// agrega su propio esquema de autenticacion: cookies en el panel Admin,
    /// JWT en la API.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Falta la cadena de conexión ConnectionStrings:DefaultConnection.");

        services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders()
            .AddErrorDescriber<SpanishIdentityErrorDescriber>();

        // El enlace de "olvide mi contraseña" vence en una hora (por defecto Identity da un dia).
        services.Configure<DataProtectionTokenProviderOptions>(options =>
            options.TokenLifespan = TimeSpan.FromMinutes(Application.Services.Notifications.EmailTemplates.PasswordResetMinutes));

        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ISaleRepository, SaleRepository>();
        services.AddScoped<IDashboardQuery, DashboardQuery>();

        services.AddScoped<IUserService, IdentityUserService>();

        // Tokens JWT: solo la API los emite, pero registrarlo aqui deja el
        // contenedor completo en ambos hosts. La API valida la clave al arrancar.
        services.AddOptions<JwtOptions>().Bind(configuration.GetSection(JwtOptions.SectionName));
        services.AddSingleton<ITokenService, JwtTokenService>();

        services.AddEmail(configuration);

        return services;
    }

    /// <summary>
    /// SMTP real si hay credenciales (Email:Smtp:UserName/Password, en user-secrets
    /// o variables de entorno); si no, o con Email:Provider=Log, los correos van al log.
    /// </summary>
    private static void AddEmail(this IServiceCollection services, IConfiguration configuration)
    {
        var seccion = configuration.GetSection(EmailOptions.SectionName);
        services.Configure<EmailOptions>(seccion);

        var opciones = seccion.Get<EmailOptions>() ?? new EmailOptions();
        var usarSmtp = string.Equals(opciones.Provider, "Smtp", StringComparison.OrdinalIgnoreCase) && opciones.SmtpConfigured;

        if (usarSmtp)
        {
            services.AddScoped<IEmailSender, SmtpEmailSender>();
        }
        else
        {
            services.AddScoped<IEmailSender, LogEmailSender>();
        }
    }

    /// <summary>Recibos PDF como archivos en una carpeta del host (el panel usa wwwroot/recibos).</summary>
    public static IServiceCollection AddFileReceiptStorage(this IServiceCollection services, string directory)
        => services.AddSingleton<IReceiptStorage>(new FileSystemReceiptStorage(directory));
}
