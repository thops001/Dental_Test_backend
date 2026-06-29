using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using dental_backend.Models;
using Microsoft.Extensions.Options;

namespace dental_backend.Dentally;

public sealed class DentallyClient : IDentallyClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly DentallyOptions _options;
    private readonly ILogger<DentallyClient> _logger;

    public DentallyClient(HttpClient http, IOptions<DentallyOptions> options, ILogger<DentallyClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Appointment>> GetAppointmentsAsync(
        string token, DateRange range, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new DentallyApiException("A Dentally API token is required.", (int)HttpStatusCode.Unauthorized);

        int maxAttempts = Math.Max(_options.CountReconciliationRetries, 0) + 1;
        List<DentallyAppointment> dtos;

        // Fetch the whole range, then reconcile the unique count against Dentally's reported total.
        // Retry on mismatch (a record may have changed mid-sync); abort if it never reconciles.
        for (int attempt = 1; ; attempt++)
        {
            var fetched = await FetchAndDeduplicateAsync(token, range, ct).ConfigureAwait(false);

            if (!fetched.HasTotal || fetched.Items.Count == fetched.Total)
            {
                dtos = fetched.Items;
                _logger.LogInformation("Fetched {Count} appointments (Dentally reported {Total}).",
                    dtos.Count, fetched.HasTotal ? fetched.Total : dtos.Count);
                break;
            }

            if (attempt >= maxAttempts)
                throw new DentallyApiException(
                    $"Sync count mismatch for {range.From:yyyy-MM-dd}..{range.To:yyyy-MM-dd}: fetched " +
                    $"{fetched.Items.Count} unique appointment(s) but Dentally reported {fetched.Total} " +
                    $"after {attempt} attempt(s). Aborting to avoid saving inaccurate data.");

            _logger.LogWarning("Count mismatch (got {Actual}, expected {Expected}); retrying (attempt {Next}/{Max}).",
                fetched.Items.Count, fetched.Total, attempt + 1, maxAttempts);
        }

        var names = await ResolveMissingPatientNamesAsync(token, dtos, ct).ConfigureAwait(false);

        return dtos
            .Select(dto => AppointmentMapper.ToAppointment(
                dto, dto.PatientId.HasValue ? names.GetValueOrDefault(dto.PatientId.Value) : null))
            .ToList();
    }

    private readonly record struct FetchedRange(List<DentallyAppointment> Items, int Total, bool HasTotal);

    /// <summary>Fetches every page for the range concurrently and de-duplicates by appointment id.</summary>
    private async Task<FetchedRange> FetchAndDeduplicateAsync(string token, DateRange range, CancellationToken ct)
    {
        // Fetch page 1 first to discover the total record count, then derive the page count.
        var first = await FetchPageAsync(token, range, page: 1, ct).ConfigureAwait(false);
        bool hasTotal = first.Meta is not null;
        int total = first.Meta?.Total ?? first.Appointments.Count;
        int pageSize = Math.Max(_options.PageSize, 1);
        int totalPages = total > 0 ? (int)Math.Ceiling((double)total / pageSize) : 1;

        var bucket = new List<DentallyAppointment>[totalPages];
        bucket[0] = first.Appointments;

        if (totalPages > 1)
        {
            using var gate = new SemaphoreSlim(Math.Max(_options.MaxConcurrency, 1));
            var tasks = Enumerable.Range(2, totalPages - 1).Select(async page =>
            {
                await gate.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    var p = await FetchPageAsync(token, range, page, ct).ConfigureAwait(false);
                    bucket[page - 1] = p.Appointments;
                }
                finally
                {
                    gate.Release();
                }
            });

            // If any page fails the aggregated task faults and we surface it — nothing is saved.
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        // De-duplicate by id while preserving page order; guards against overlapping pages.
        var seen = new HashSet<string>();
        var dtos = new List<DentallyAppointment>();
        foreach (var page in bucket)
        {
            if (page is null) continue;
            foreach (var dto in page)
            {
                var id = dto.Id.HasValue ? dto.Id.Value : Guid.NewGuid().ToString();
                if (seen.Add(id))
                    dtos.Add(dto);
            }
        }

        return new FetchedRange(dtos, total, hasTotal);
    }

    public async Task<AppointmentDetail?> GetAppointmentDetailAsync(string token, string id, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new DentallyApiException("A Dentally API token is required.", (int)HttpStatusCode.Unauthorized);

        var envelope = await GetJsonAsync<DentallySingleAppointment>(
            $"appointments/{Uri.EscapeDataString(id)}", token, $"appointment {id}", ct).ConfigureAwait(false);

        var appt = envelope?.Appointment;
        if (appt is null) return null;

        var detail = new AppointmentDetail
        {
            Id = appt.Id.HasValue ? appt.Id.Value : id,
            Date = (appt.StartTime ?? default).ToUniversalTime(),
            FinishTime = appt.FinishTime?.ToUniversalTime(),
            Duration = appt.Duration ?? (appt is { StartTime: { } s, FinishTime: { } f }
                ? (int)Math.Round((f - s).TotalMinutes) : 0),
            PatientName = appt.PatientName?.Trim() ?? string.Empty,
            PractitionerName = appt.PractitionerName?.Trim() ?? string.Empty,
            Reason = appt.Reason ?? string.Empty,
            Status = appt.State ?? string.Empty,
            Notes = appt.Notes,
            CancelledAt = appt.CancelledAt?.ToUniversalTime()
        };

        if (appt.PatientId.HasValue)
        {
            var patient = await TryGetPatientAsync(token, appt.PatientId.Value, ct).ConfigureAwait(false);
            if (patient is not null)
            {
                if (string.IsNullOrWhiteSpace(detail.PatientName))
                    detail.PatientName = patient.FullName;
                detail.Patient = new PatientContact
                {
                    Id = patient.Id.HasValue ? patient.Id.Value : appt.PatientId.Value,
                    Email = patient.EmailAddress,
                    Phone = string.IsNullOrWhiteSpace(patient.MobilePhone) ? patient.HomePhone : patient.MobilePhone,
                    DateOfBirth = patient.DateOfBirth
                };
            }
        }

        return detail;
    }

    /// <summary>Looks up names for appointments that came back without a patient_name (best-effort).</summary>
    private async Task<Dictionary<string, string>> ResolveMissingPatientNamesAsync(
        string token, List<DentallyAppointment> dtos, CancellationToken ct)
    {
        if (!_options.EnrichPatientNames)
            return new Dictionary<string, string>();

        var ids = dtos
            .Where(d => string.IsNullOrWhiteSpace(d.PatientName) && d.PatientId.HasValue)
            .Select(d => d.PatientId.Value)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
            return new Dictionary<string, string>();

        var names = new ConcurrentDictionary<string, string>();
        using var gate = new SemaphoreSlim(Math.Max(_options.MaxConcurrency, 1));
        await Task.WhenAll(ids.Select(async patientId =>
        {
            await gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var patient = await TryGetPatientAsync(token, patientId, ct).ConfigureAwait(false);
                if (patient is not null && !string.IsNullOrWhiteSpace(patient.FullName))
                    names[patientId] = patient.FullName;
            }
            finally
            {
                gate.Release();
            }
        })).ConfigureAwait(false);

        _logger.LogInformation("Enriched {Count}/{Total} missing patient name(s).", names.Count, ids.Count);
        return new Dictionary<string, string>(names);
    }

    /// <summary>Patient lookups are best-effort — a failure must not abort an otherwise-good sync.</summary>
    private async Task<DentallyPatient?> TryGetPatientAsync(string token, string patientId, CancellationToken ct)
    {
        try
        {
            var response = await GetJsonAsync<DentallyPatientResponse>(
                $"patients/{Uri.EscapeDataString(patientId)}", token, $"patient {patientId}", ct).ConfigureAwait(false);
            return response?.Patient;
        }
        catch (DentallyApiException ex)
        {
            _logger.LogWarning("Patient {Id} lookup failed: {Message}", patientId, ex.Message);
            return null;
        }
    }

    private async Task<DentallyAppointmentsPage> FetchPageAsync(
        string token, DateRange range, int page, CancellationToken ct)
    {
        // Dentally filters appointments by start time using `after` / `before` (inclusive day range).
        var after = Uri.EscapeDataString($"{range.From:yyyy-MM-dd}T00:00:00Z");
        var before = Uri.EscapeDataString($"{range.To:yyyy-MM-dd}T23:59:59Z");
        var url = $"appointments?after={after}&before={before}&page={page}&per_page={_options.PageSize}";

        return await GetJsonAsync<DentallyAppointmentsPage>(url, token, $"page {page}", ct).ConfigureAwait(false)
               ?? throw new DentallyApiException($"Empty response body on page {page}.");
    }

    private async Task<T?> GetJsonAsync<T>(string url, string token, string context, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            throw new DentallyApiException($"Network error contacting Dentally ({context}).", null, ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var body = await SafeReadAsync(response, ct).ConfigureAwait(false);
                throw new DentallyApiException(
                    $"Dentally returned {(int)response.StatusCode} ({context}): {body}",
                    (int)response.StatusCode);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, ct).ConfigureAwait(false);
        }
    }

    private static async Task<string> SafeReadAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return text.Length > 500 ? text[..500] : text;
        }
        catch
        {
            return "<unreadable body>";
        }
    }
}
