namespace InfectionSolutions.Api.Common;

public static class AuthorizationPolicies
{
    /// <summary>Gestion del negocio: CRUD de productos y clientes, todas las ventas.</summary>
    public const string AdminOnly = "AdminOnly";

    /// <summary>Portal: compras propias y sus comprobantes.</summary>
    public const string CustomerOnly = "CustomerOnly";
}
