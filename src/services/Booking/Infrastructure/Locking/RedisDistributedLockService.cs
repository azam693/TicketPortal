using StackExchange.Redis;

namespace Booking.Infrastructure.Locking;

public class RedisDistributedLockService(IConnectionMultiplexer redis) : IDistributedLockService
{
    // Release проверяет владельца токеном, чтобы не снять чужой лок,
    // взятый уже после истечения TTL текущего держателя.
    private static readonly LuaScript ReleaseScript = LuaScript.Prepare(
        "if redis.call('get', @key) == @token then return redis.call('del', @key) else return 0 end");

    public async Task<IAsyncDisposable?> TryAcquireAsync(string key, TimeSpan expiry, CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();
        var token = Guid.NewGuid().ToString();

        var acquired = await db.StringSetAsync(key, token, expiry, When.NotExists);

        return acquired ? new RedisLock(db, key, token) : null;
    }

    private sealed class RedisLock(IDatabase db, string key, string token) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() =>
            new(db.ScriptEvaluateAsync(ReleaseScript, new { key = (RedisKey)key, token = (RedisValue)token }));
    }
}
