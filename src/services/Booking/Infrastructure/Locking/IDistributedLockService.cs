namespace Booking.Infrastructure.Locking;

public interface IDistributedLockService
{
    /// <returns>Держатель лока для release через DisposeAsync, либо null, если лок занят.</returns>
    Task<IAsyncDisposable?> TryAcquireAsync(string key, TimeSpan expiry, CancellationToken cancellationToken);
}
