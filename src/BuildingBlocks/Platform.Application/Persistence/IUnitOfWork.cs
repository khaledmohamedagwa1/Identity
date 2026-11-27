namespace Platform.Application.Persistence;

/// <summary>
/// Commits or rolls back everything a command changed, atomically: aggregate rows, outbox rows and
/// audit events. Implemented by Infrastructure over the module DbContext (F-03).
/// </summary>
public interface IUnitOfWork
{
    /// <summary>Starts the unit of work. Database transactions begin lazily on the first save.</summary>
    Task BeginAsync(CancellationToken cancellationToken);

    /// <summary>Saves pending changes and commits the transaction.</summary>
    /// <exception cref="ConcurrencyConflictException">Another writer changed the same aggregate.</exception>
    Task CommitAsync(CancellationToken cancellationToken);

    /// <summary>Discards pending changes and rolls back any open transaction.</summary>
    Task RollbackAsync(CancellationToken cancellationToken);
}

/// <summary>Raised by <see cref="IUnitOfWork.CommitAsync"/> on an optimistic concurrency conflict.</summary>
public sealed class ConcurrencyConflictException : Exception
{
    /// <summary>Creates the exception.</summary>
    public ConcurrencyConflictException()
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    public ConcurrencyConflictException(string message) : base(message)
    {
    }

    /// <summary>Creates the exception with a message and inner exception.</summary>
    public ConcurrencyConflictException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>Used when no persistence is registered (pure application tests).</summary>
internal sealed class NoOpUnitOfWork : IUnitOfWork
{
    public Task BeginAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task RollbackAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
