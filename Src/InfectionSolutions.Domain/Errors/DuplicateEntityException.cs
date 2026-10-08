namespace InfectionSolutions.Domain.Errors;

public class DuplicateEntityException : DomainException
{
    public DuplicateEntityException(string message) : base(message)
    {
    }
}
