namespace InfectionSolutions.Application.Common;

/// <summary>Datos de la empresa que aparecen en recibos y reportes (seccion "Company" de appsettings).</summary>
public sealed class CompanyOptions
{
    public const string SectionName = "Company";

    public string Name { get; set; } = "InfectionSolutions";

    public string Tagline { get; set; } = "Materiales de construcción";

    public string TaxId { get; set; } = "900.000.000-0";

    public string Address { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;
}
