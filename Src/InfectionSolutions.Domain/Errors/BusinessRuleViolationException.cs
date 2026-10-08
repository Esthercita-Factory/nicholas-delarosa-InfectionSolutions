namespace InfectionSolutions.Domain.Errors;

public class BusinessRuleViolationException : DomainException
{
    public BusinessRuleViolationException(string message) : base(message)
    {
    }
}
