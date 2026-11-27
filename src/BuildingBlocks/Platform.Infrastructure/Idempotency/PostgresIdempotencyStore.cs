using Microsoft.EntityFrameworkCore;
using Platform.Application.Idempotency;
using Platform.Infrastructure.Persistence;

namespace Platform.Infrastructure.Idempotency;

/// <summary>Idempotency keys in <c>platform.idempotency_keys</c>; the first writer of a key wins.</summary>
public sealed class PostgresIdempotencyStore(PlatformDbContext db, TimeProvider clock) : IIdempotencyStore
{
    /// <inheritdoc />
    public async Task<StoredResponse?> GetAsync(string key, CancellationToken cancellationToken)
    {
        var record = await db.Set<IdempotencyRecord>().AsNoTracking()
            .SingleOrDefaultAsync(r => r.Key == key, cancellationToken).ConfigureAwait(false);
        return record is null ? null : new StoredResponse(record.ResponseType, record.ValueJson);
    }

    /// <inheritdoc />
    public async Task SaveAsync(string key, StoredResponse response, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);
        var now = clock.GetUtcNow();
        await db.Database.ExecuteSqlAsync(
            $"INSERT INTO platform.idempotency_keys (key, response_type, value_json, created_at) VALUES ({key}, {response.ResponseType}, CAST({response.ValueJson} AS jsonb), {now}) ON CONFLICT (key) DO NOTHING",
            cancellationToken).ConfigureAwait(false);
    }
}
