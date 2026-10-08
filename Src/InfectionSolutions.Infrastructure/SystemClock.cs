using InfectionSolutions.Application.Abstractions;

namespace InfectionSolutions.Infrastructure;

/// <summary>Npgsql exige offset 0 para timestamptz: siempre UTC.</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
