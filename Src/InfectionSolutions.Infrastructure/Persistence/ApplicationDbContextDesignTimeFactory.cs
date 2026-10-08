using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace InfectionSolutions.Infrastructure.Persistence;

/// <summary>
/// Permite usar dotnet ef con Infrastructure como proyecto de inicio, sin
/// compilar ni levantar el panel (util si el Admin esta corriendo). Para
/// "migrations add" no hace falta conexion; para "database update" se toma
/// de la variable ConnectionStrings__DefaultConnection.
/// </summary>
public sealed class ApplicationDbContextDesignTimeFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=infection_solutions;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new ApplicationDbContext(options);
    }
}
