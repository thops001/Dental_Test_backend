using dental_backend.Models;

namespace dental_backend.Dentally;

public interface IDentallyClient
{
    /// <summary>
    /// Fetches every appointment within the (inclusive) date range, retrieving all pages.
    /// Pages after the first are fetched concurrently. If any page ultimately fails the whole
    /// call throws so callers never persist a partial result.
    /// </summary>
    Task<IReadOnlyList<Appointment>> GetAppointmentsAsync(
        string token, DateRange range, CancellationToken ct);

    /// <summary>Fetches the full detail for a single appointment (incl. patient contact), or null if not found.</summary>
    Task<AppointmentDetail?> GetAppointmentDetailAsync(string token, string id, CancellationToken ct);
}

/// <summary>Raised when Dentally returns a non-success response we cannot recover from.</summary>
public sealed class DentallyApiException : Exception
{
    public int? StatusCode { get; }

    public DentallyApiException(string message, int? statusCode = null, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
    }
}
