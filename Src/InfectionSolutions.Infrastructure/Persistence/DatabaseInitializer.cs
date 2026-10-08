using InfectionSolutions.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace InfectionSolutions.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    /// <summary>
    /// Aplica las migraciones pendientes (crea la base si no existe) y siembra
    /// roles y administrador. La estructura sale solo de las migraciones de EF
    /// Core, nunca de scripts SQL.
    /// </summary>
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseInitializer));
        var db = provider.GetRequiredService<ApplicationDbContext>();

        var pendientes = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pendientes.Count > 0)
        {
            logger.LogInformation("Aplicando {Cantidad} migraciones: {Migraciones}", pendientes.Count, string.Join(", ", pendientes));
        }

        await db.Database.MigrateAsync(cancellationToken);
        await IdentitySeeder.SeedAsync(provider);
    }
}
