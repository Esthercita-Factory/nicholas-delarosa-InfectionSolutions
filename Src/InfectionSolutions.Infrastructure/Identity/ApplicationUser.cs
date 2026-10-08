using Microsoft.AspNetCore.Identity;

namespace InfectionSolutions.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
}
