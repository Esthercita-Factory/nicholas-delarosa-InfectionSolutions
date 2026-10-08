namespace InfectionSolutions.Application.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
