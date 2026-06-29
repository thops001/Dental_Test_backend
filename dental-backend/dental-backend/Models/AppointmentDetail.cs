namespace dental_backend.Models;

/// <summary>
/// Richer view of a single appointment for the detail modal. Extends the core fields with
/// data fetched live from Dentally (finish time, notes, cancellation, patient contact).
/// </summary>
public sealed class AppointmentDetail
{
    public required string Id { get; set; }
    public required DateTimeOffset Date { get; set; }
    public DateTimeOffset? FinishTime { get; set; }
    public int Duration { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string PractitionerName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public PatientContact? Patient { get; set; }
}

public sealed class PatientContact
{
    public string? Id { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? DateOfBirth { get; set; }
}
