using Microsoft.EntityFrameworkCore;

namespace Platform.Infrastructure.Inbox;

/// <summary>Idempotent consumers: process each integration event at most once per consumer (TC-F03-04).</summary>
public static class InboxExtensions
{
    /// <summary>
    /// Records that <paramref name="consumer"/> handles <paramref name="eventId"/>. Returns false when it was
    /// already processed. Call it inside the consumer's transaction, before its work, so the inbox row and the
    /// work commit or roll back together.
    /// </summary>
    public static async Task<bool> TryAcceptInboxAsync(this DbContext db, Guid eventId, string consumer, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentException.ThrowIfNullOrWhiteSpace(consumer);
        var inserted = await db.Database.ExecuteSqlAsync(
            $"INSERT INTO platform.inbox (event_id, consumer, processed_at) VALUES ({eventId}, {consumer}, {now}) ON CONFLICT DO NOTHING",
            cancellationToken).ConfigureAwait(false);
        return inserted == 1;
    }
}
