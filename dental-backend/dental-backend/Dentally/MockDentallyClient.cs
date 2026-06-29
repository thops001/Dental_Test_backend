using dental_backend.Models;

namespace dental_backend.Dentally;

/// <summary>
/// Generates deterministic, realistic appointment data for the requested range so the app can
/// be exercised end-to-end without a live Dentally token. Enabled via "Dentally:UseMock".
/// </summary>
public sealed class MockDentallyClient : IDentallyClient
{
    private static readonly string[] Patients =
    {
        "Olivia Bennett", "Liam Carter", "Sophia Patel", "Noah Williams", "Ava Thompson",
        "James Murphy", "Isabella Rossi", "Lucas Nguyen", "Mia Andersson", "Ethan O'Brien"
    };

    private static readonly string[] Practitioners =
    {
        "Dr. Sarah Hughes", "Dr. Raj Mehta", "Dr. Emily Clarke", "Dr. Daniel Foster"
    };

    private static readonly string[] Reasons =
    {
        "Routine check-up", "Scale and polish", "Filling", "Root canal", "Extraction",
        "Crown fitting", "Emergency", "Whitening consultation"
    };

    private static readonly string[] Statuses = { "Booked", "Arrived", "Complete", "Cancelled" };

    public Task<IReadOnlyList<Appointment>> GetAppointmentsAsync(
        string token, DateRange range, CancellationToken ct)
    {
        var result = new List<Appointment>();

        for (var day = range.From; day <= range.To; day = day.AddDays(1))
        {
            // Deterministic per-day count derived from the date — no randomness.
            int perDay = 6 + (day.DayNumber % 7);
            for (int i = 0; i < perDay; i++)
            {
                ct.ThrowIfCancellationRequested();
                int slot = i % 16;
                var start = new DateTimeOffset(day.Year, day.Month, day.Day, 9 + slot / 2, slot % 2 * 30, 0, TimeSpan.Zero);
                int seed = day.DayNumber + i;

                result.Add(new Appointment
                {
                    Id = $"mock-{day:yyyyMMdd}-{i}",
                    Date = start,
                    Duration = new[] { 15, 20, 30, 45, 60 }[seed % 5],
                    PatientName = Patients[seed % Patients.Length],
                    PractitionerName = Practitioners[seed % Practitioners.Length],
                    Reason = Reasons[seed % Reasons.Length],
                    Status = Statuses[seed % Statuses.Length]
                });
            }
        }

        return Task.FromResult<IReadOnlyList<Appointment>>(result);
    }

    public Task<AppointmentDetail?> GetAppointmentDetailAsync(string token, string id, CancellationToken ct)
    {
        // The mock has no separate detail resource; the service falls back to the cached record.
        return Task.FromResult<AppointmentDetail?>(null);
    }
}
