namespace InfectionSolutions.Domain.Errors;

public class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }
}
