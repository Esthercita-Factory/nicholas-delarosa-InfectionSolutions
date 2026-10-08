namespace InfectionSolutions.Domain.Identity;

public static class ApplicationRoles
{
    /// <summary>Accede al panel Razor (InfectionSolutions.Admin).</summary>
    public const string Administrator = "Administrador";

    /// <summary>Pertenece al sistema pero solo entra por el portal de clientes (SPA).</summary>
    public const string Customer = "Cliente";
}
