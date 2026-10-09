using InfectionSolutions.Application.Abstractions;
using InfectionSolutions.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace InfectionSolutions.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    /// <summary>
    /// Clave del candado de PostgreSQL (pg_advisory_lock) que serializa la
    /// inicializacion: en Docker el panel y la API arrancan a la vez sobre la
    /// misma base, y sin el candado los dos intentarian crear las mismas tablas.
    /// </summary>
    private const long LockKey = 7_412_021_001;

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

        // Si la base aun no existe (primer arranque local) no hay donde tomar el
        // candado: MigrateAsync la crea y en ese caso solo hay una app arrancando.
        var conCandado = await db.Database.CanConnectAsync(cancellationToken);
        if (conCandado)
        {
            await db.Database.OpenConnectionAsync(cancellationToken);
            await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_lock({LockKey})", cancellationToken);
        }

        try
        {
            var pendientes = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
            if (pendientes.Count > 0)
            {
                logger.LogInformation("Aplicando {Cantidad} migraciones: {Migraciones}", pendientes.Count, string.Join(", ", pendientes));
            }

            await db.Database.MigrateAsync(cancellationToken);
            await IdentitySeeder.SeedAsync(provider);

            if (provider.GetRequiredService<IConfiguration>().GetValue("SeedDemoData", false))
            {
                await DemoDataSeeder.SeedAsync(db, provider.GetRequiredService<IClock>(), logger, cancellationToken);
            }
        }
        finally
        {
            if (conCandado)
            {
                await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_unlock({LockKey})", CancellationToken.None);
                await db.Database.CloseConnectionAsync();
            }
        }
    }
}
