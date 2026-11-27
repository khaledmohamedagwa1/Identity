using Microsoft.EntityFrameworkCore;
using Platform.Application.Events;
using Platform.Application.Persistence;

namespace Platform.Infrastructure.Persistence;

/// <summary>
/// EF Core unit of work. While active, the first save of a module context opens a database
/// transaction; <see cref="CommitAsync"/> saves remaining changes (including queued outbox events)
/// and commits; <see cref="RollbackAsync"/> rolls back and clears tracked changes.
/// A command may write to one module's context only (section 6.5: no cross-module writes).
/// </summary>
public sealed class EfUnitOfWork(OutboxBuffer outbox) : IUnitOfWork
{
    private readonly List<DbContext> _enlisted = [];
    private DbContext? _writer;

    /// <summary>True between <see cref="BeginAsync"/> and commit or rollback.</summary>
    public bool IsActive { get; private set; }

    internal void Enlist(DbContext context) => _enlisted.Add(context);

    /// <inheritdoc />
    public Task BeginAsync(CancellationToken cancellationToken)
    {
        IsActive = true;
        _writer = null;
        return Task.CompletedTask;
    }

    internal async Task EnsureTransactionAsync(DbContext context, CancellationToken cancellationToken)
    {
        if (!IsActive)
        {
            return;
        }

        if (_writer is not null && !ReferenceEquals(_writer, context))
        {
            throw new InvalidOperationException(
                $"A command may change one module only, but both '{_writer.GetType().Name}' and '{context.GetType().Name}' were written. Use integration events instead.");
        }

        _writer = context;
        if (context.Database.CurrentTransaction is null)
        {
            await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        if (!IsActive)
        {
            return;
        }

        var target = _writer
            ?? _enlisted.SingleOrDefault(c => c.ChangeTracker.HasChanges())
            ?? (outbox.Pending.Count > 0 && _enlisted.Count == 1 ? _enlisted[0] : null);

        if (target is null && outbox.Pending.Count > 0)
        {
            throw new InvalidOperationException("Integration events were queued but no module DbContext is available to persist them.");
        }

        if (target is not null && (target.ChangeTracker.HasChanges() || outbox.Pending.Count > 0))
        {
            await target.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        if (target?.Database.CurrentTransaction is { } transaction)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            await transaction.DisposeAsync().ConfigureAwait(false);
        }

        IsActive = false;
        _writer = null;
    }

    /// <inheritdoc />
    public async Task RollbackAsync(CancellationToken cancellationToken)
    {
        foreach (var context in _enlisted)
        {
            if (context.Database.CurrentTransaction is { } transaction)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                await transaction.DisposeAsync().ConfigureAwait(false);
            }

            context.ChangeTracker.Clear();
        }

        outbox.Clear();
        IsActive = false;
        _writer = null;
    }
}
