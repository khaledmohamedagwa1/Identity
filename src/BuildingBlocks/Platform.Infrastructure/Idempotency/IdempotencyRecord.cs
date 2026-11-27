namespace Platform.Infrastructure.Idempotency;

/// <summary>A row of <c>platform.idempotency_keys</c>.</summary>
public sealed class IdempotencyRecord
{
    private IdempotencyRecord()
    {
        Key = ResponseType = ValueJson = string.Empty;
    }

    /// <summary>Creates a record.</summary>
    public IdempotencyRecord(string key, string responseType, string valueJson, DateTimeOffset createdAt)
    {
        Key = key;
        ResponseType = responseType;
        ValueJson = valueJson;
        CreatedAt = createdAt;
    }

    /// <summary>Scoped key: tenant, command type and client key.</summary>
    public string Key { get; private set; }

    /// <summary>CLR type of the stored response.</summary>
    public string ResponseType { get; private set; }

    /// <summary>The stored response as JSON.</summary>
    public string ValueJson { get; private set; }

    /// <summary>When it was stored; a cleanup job removes old keys.</summary>
    public DateTimeOffset CreatedAt { get; private set; }
}
