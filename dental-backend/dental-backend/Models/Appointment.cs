namespace dental_backend.Models;

/// <summary>
/// Normalised appointment as persisted in the local JSON cache and returned to the frontend.
/// </summary>
public sealed class Appointment
{
    public required string Id { get; init; }

    /// <summary>Appointment start date and time in UTC.</summary>
    public required DateTimeOffset Date { get; init; }

    /// <summary>Length of the appointment in minutes.</summary>
    public int Duration { get; init; }

    public string PatientName { get; init; } = string.Empty;

    public string PractitionerName { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;
}
