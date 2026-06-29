using dental_backend.Models;

namespace dental_backend.Services;

public interface ISyncService
{
    /// <summary>Fetches appointments from Dentally for the requested range and overwrites the cache.</summary>
    Task<SyncResult> SyncAsync(SyncRequest request, CancellationToken ct);

    /// <summary>Returns the most recently synced result, or null if no sync has run.</summary>
    Task<SyncResult?> GetCachedAsync(CancellationToken ct);

    /// <summary>Returns a single cached appointment by id, or null if it is not in the cache.</summary>
    Task<Appointment?> GetAppointmentAsync(string id, CancellationToken ct);

    /// <summary>
    /// Returns the full detail for a single appointment: the cached core data enriched with
    /// live Dentally fields (finish time, notes, cancellation, patient contact) when available.
    /// </summary>
    Task<AppointmentDetail?> GetAppointmentDetailAsync(string id, CancellationToken ct);
}
