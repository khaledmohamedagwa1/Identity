using System.Collections.Concurrent;

namespace Platform.Application.Idempotency;

/// <summary>A previously stored successful result, serialized as JSON.</summary>
/// <param name="ResponseType">CLR type name of the stored value, to detect key reuse across commands.</param>
/// <param name="ValueJson">The serialized value.</param>
public sealed record StoredResponse(string ResponseType, string ValueJson);

/// <summary>Stores command results by idempotency key. Keys are scoped by tenant and command type.</summary>
public interface IIdempotencyStore
{
    /// <summary>Returns the stored response for <paramref name="key"/>, or <c>null</c>.</summary>
    Task<StoredResponse?> GetAsync(string key, CancellationToken cancellationToken);

    /// <summary>Stores the response for <paramref name="key"/>.</summary>
    Task SaveAsync(string key, StoredResponse response, CancellationToken cancellationToken);
}

/// <summary>Process-local store for tests and single-instance development. Production uses the PostgreSQL store.</summary>
public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly ConcurrentDictionary<string, StoredResponse> _items = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task<StoredResponse?> GetAsync(string key, CancellationToken cancellationToken) =>
        Task.FromResult(_items.TryGetValue(key, out var value) ? value : null);

    /// <inheritdoc />
    public Task SaveAsync(string key, StoredResponse response, CancellationToken cancellationToken)
    {
        _items.TryAdd(key, response);
        return Task.CompletedTask;
    }
}
