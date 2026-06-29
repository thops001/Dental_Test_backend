namespace dental_backend.Models;

/// <summary>Request body for triggering a sync from the frontend.</summary>
public sealed class SyncRequest
{
    /// <summary>Dentally bearer token. Optional — falls back to the server-configured token when omitted.</summary>
    public string? Token { get; init; }

    public DateRangePreset Range { get; init; } = DateRangePreset.Today;

    /// <summary>Inclusive start date (UTC) — required when <see cref="Range"/> is Custom.</summary>
    public DateOnly? From { get; init; }

    /// <summary>Inclusive end date (UTC) — required when <see cref="Range"/> is Custom.</summary>
    public DateOnly? To { get; init; }
}

/// <summary>Outcome of a sync, returned to the frontend and used as the cache envelope on disk.</summary>
public sealed class SyncResult
{
    public required int RecordCount { get; init; }
    public required double DurationMs { get; init; }
    public required DateTimeOffset SyncedAt { get; init; }
    public required DateOnly From { get; init; }
    public required DateOnly To { get; init; }
    public required IReadOnlyList<Appointment> Appointments { get; init; }
}
