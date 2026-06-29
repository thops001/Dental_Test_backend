using dental_backend.Dentally;
using dental_backend.Models;
using dental_backend.Storage;

namespace dental_backend.Tests;

/// <summary>A TimeProvider that always reports a fixed instant.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

internal sealed class FakeDentallyClient : IDentallyClient
{
    private readonly IReadOnlyList<Appointment>? _result;
    private readonly Exception? _throw;

    public FakeDentallyClient(IReadOnlyList<Appointment> result) => _result = result;
    public FakeDentallyClient(Exception toThrow) => _throw = toThrow;

    public string? LastToken { get; private set; }
    public DateRange LastRange { get; private set; }

    public Task<IReadOnlyList<Appointment>> GetAppointmentsAsync(string token, DateRange range, CancellationToken ct)
    {
        LastToken = token;
        LastRange = range;
        if (_throw is not null) throw _throw;
        return Task.FromResult(_result!);
    }

    public AppointmentDetail? DetailToReturn { get; set; }

    public Task<AppointmentDetail?> GetAppointmentDetailAsync(string token, string id, CancellationToken ct) =>
        Task.FromResult(DetailToReturn);
}

internal sealed class InMemoryStore : IAppointmentStore
{
    public SyncResult? Saved { get; private set; }
    public int SaveCount { get; private set; }

    public Task SaveAsync(SyncResult result, CancellationToken ct)
    {
        Saved = result;
        SaveCount++;
        return Task.CompletedTask;
    }

    public Task<SyncResult?> ReadAsync(CancellationToken ct) => Task.FromResult(Saved);
}
