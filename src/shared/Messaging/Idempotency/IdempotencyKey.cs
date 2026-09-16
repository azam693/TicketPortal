namespace Messaging.Idempotency;

/// <summary>
/// Идемпотентность запроса клиента (Idempotency-Key), отдельная от
/// транспортной идемпотентности шины (InboxMessage.Id).
/// </summary>
public class IdempotencyKey
{
    public string Key { get; private set; }
    public string RequestHash { get; private set; }
    public int ResponseStatusCode { get; private set; }
    public string ResponseBody { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }

    private IdempotencyKey()
    {
    }

    public IdempotencyKey(
        string key,
        string requestHash,
        int responseStatusCode,
        string responseBody,
        TimeSpan ttl)
    {
        Key = key;
        RequestHash = requestHash;
        ResponseStatusCode = responseStatusCode;
        ResponseBody = responseBody;
        CreatedAt = DateTimeOffset.UtcNow;
        ExpiresAt = CreatedAt.Add(ttl);
    }
}
