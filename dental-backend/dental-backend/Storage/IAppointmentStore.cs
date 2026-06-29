using dental_backend.Models;

namespace dental_backend.Storage;

public interface IAppointmentStore
{
    /// <summary>Atomically overwrites the local cache with the supplied sync result.</summary>
    Task SaveAsync(SyncResult result, CancellationToken ct);

    /// <summary>Reads the cached sync result, or null when no sync has been performed yet.</summary>
    Task<SyncResult?> ReadAsync(CancellationToken ct);
}
