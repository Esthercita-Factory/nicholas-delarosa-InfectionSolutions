namespace InfectionSolutions.Domain.Entities;

public class Customer
{
    public Guid Id { get; set; }
    public string Document { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int Age { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Usuario de Identity con rol Cliente, cuando el cliente tiene acceso al
    /// portal. Los clientes creados desde el panel no lo tienen.
    /// </summary>
    public string? UserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public ICollection<Sale> Sales { get; set; } = new List<Sale>();
}
