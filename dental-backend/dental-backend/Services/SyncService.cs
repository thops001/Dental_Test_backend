using System.Diagnostics;
using dental_backend.Dentally;
using dental_backend.Models;
using dental_backend.Storage;
using Microsoft.Extensions.Options;

namespace dental_backend.Services;

public sealed class SyncService : ISyncService
{
    private readonly IDentallyClient _client;
    private readonly IAppointmentStore _store;
    private readonly DentallyOptions _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<SyncService> _logger;

    public SyncService(
        IDentallyClient client,
        IAppointmentStore store,
        IOptions<DentallyOptions> options,
        TimeProvider clock,
        ILogger<SyncService> logger)
    {
        _client = client;
        _store = store;
        _options = options.Value;
        _clock = clock;
        _logger = logger;
    }

    public async Task<SyncResult> SyncAsync(SyncRequest request, CancellationToken ct)
    {
        var token = !string.IsNullOrWhiteSpace(request.Token) ? request.Token! : _options.Token ?? string.Empty;
        var range = DateRange.Resolve(request.Range, request.From, request.To, _clock.GetUtcNow());

        _logger.LogInformation("Starting sync for {From}..{To}.", range.From, range.To);

        var stopwatch = Stopwatch.StartNew();
        var appointments = await _client.GetAppointmentsAsync(token, range, ct).ConfigureAwait(false);
        stopwatch.Stop();

        // Stable ordering by start time so the cached file and table are deterministic.
        var ordered = appointments.OrderBy(a => a.Date).ThenBy(a => a.Id, StringComparer.Ordinal).ToList();

        var result = new SyncResult
        {
            RecordCount = ordered.Count,
            DurationMs = stopwatch.Elapsed.TotalMilliseconds,
            SyncedAt = _clock.GetUtcNow(),
            From = range.From,
            To = range.To,
            Appointments = ordered
        };

        await _store.SaveAsync(result, ct).ConfigureAwait(false);
        _logger.LogInformation("Sync complete: {Count} records in {Ms:F0} ms.", result.RecordCount, result.DurationMs);
        return result;
    }

    public Task<SyncResult?> GetCachedAsync(CancellationToken ct) => _store.ReadAsync(ct);

    public async Task<Appointment?> GetAppointmentAsync(string id, CancellationToken ct)
    {
        var cached = await _store.ReadAsync(ct).ConfigureAwait(false);
        return cached?.Appointments.FirstOrDefault(a => a.Id == id);
    }

    public async Task<AppointmentDetail?> GetAppointmentDetailAsync(string id, CancellationToken ct)
    {
        var cached = await GetAppointmentAsync(id, ct).ConfigureAwait(false);

        AppointmentDetail? live = null;
        try
        {
            // The mock client returns null here; the live client throws if no token is configured.
            live = await _client.GetAppointmentDetailAsync(_options.Token ?? string.Empty, id, ct).ConfigureAwait(false);
        }
        catch (DentallyApiException ex)
        {
            _logger.LogWarning("Live detail lookup for {Id} failed: {Message}", id, ex.Message);
        }

        if (cached is null && live is null)
            return null;

        return new AppointmentDetail
        {
            Id = id,
            Date = cached?.Date ?? live!.Date,
            // Prefer the live finish time; otherwise derive it from the cached start + duration.
            FinishTime = live?.FinishTime ?? cached?.Date.AddMinutes(cached.Duration),
            Duration = cached?.Duration ?? live?.Duration ?? 0,
            PatientName = Coalesce(cached?.PatientName, live?.PatientName),
            PractitionerName = Coalesce(cached?.PractitionerName, live?.PractitionerName),
            Reason = Coalesce(cached?.Reason, live?.Reason),
            Status = Coalesce(cached?.Status, live?.Status),
            Notes = live?.Notes,
            CancelledAt = live?.CancelledAt,
            Patient = live?.Patient
        };
    }

    private static string Coalesce(string? a, string? b) =>
        !string.IsNullOrWhiteSpace(a) ? a! : (b ?? string.Empty);
}
