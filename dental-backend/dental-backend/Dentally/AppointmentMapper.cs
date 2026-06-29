using dental_backend.Models;

namespace dental_backend.Dentally;

internal static class AppointmentMapper
{
    public static Appointment ToAppointment(DentallyAppointment src, string? patientNameOverride = null)
    {
        var start = src.StartTime ?? default;

        int duration = src.Duration
            ?? (src.FinishTime is { } finish && src.StartTime is { } s
                ? (int)Math.Round((finish - s).TotalMinutes)
                : 0);

        var patientName = !string.IsNullOrWhiteSpace(patientNameOverride)
            ? patientNameOverride!.Trim()
            : ResolveName(src.PatientName, "Patient", src.PatientId);

        return new Appointment
        {
            Id = src.Id.HasValue ? src.Id.Value : Guid.NewGuid().ToString(),
            // Normalise to UTC — Dentally timestamps are UTC but may carry an offset.
            Date = start.ToUniversalTime(),
            Duration = duration,
            PatientName = patientName,
            PractitionerName = ResolveName(src.PractitionerName, "Practitioner", src.PractitionerId),
            Reason = src.Reason ?? string.Empty,
            Status = src.State ?? string.Empty
        };
    }

    private static string ResolveName(string? explicitName, string label, JsonId id)
    {
        if (!string.IsNullOrWhiteSpace(explicitName))
            return explicitName.Trim();
        return id.HasValue ? $"{label} #{id.Value}" : string.Empty;
    }
}
