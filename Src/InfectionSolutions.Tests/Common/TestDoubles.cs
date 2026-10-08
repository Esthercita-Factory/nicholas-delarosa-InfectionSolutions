namespace InfectionSolutions.Tests.Common;

/// <summary>Reloj fijo para que las fechas de las pruebas sean deterministas.</summary>
public sealed class FixedClock : IClock
{
    public FixedClock(DateTimeOffset now)
    {
        UtcNow = now;
    }

    public DateTimeOffset UtcNow { get; }
}

/// <summary>Unidad de trabajo que no toca ninguna base: solo cuenta los guardados.</summary>
public sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public bool Committed { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.FromResult(1);
    }

    public Task<ITransactionScope> BeginTransactionAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<ITransactionScope>(new FakeTransaction(this));

    private sealed class FakeTransaction : ITransactionScope
    {
        private readonly FakeUnitOfWork _owner;

        public FakeTransaction(FakeUnitOfWork owner)
        {
            _owner = owner;
        }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            _owner.Committed = true;
            return Task.CompletedTask;
        }

        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
