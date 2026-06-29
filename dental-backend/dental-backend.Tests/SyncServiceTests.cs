using dental_backend.Dentally;
using dental_backend.Models;
using dental_backend.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace dental_backend.Tests;

public class SyncServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 24, 10, 0, 0, TimeSpan.Zero);

    private static SyncService Create(IDentallyClient client, InMemoryStore store, DentallyOptions? opts = null) =>
        new(client, store,
            Options.Create(opts ?? new DentallyOptions { Token = "config-token" }),
            new FixedTimeProvider(Now),
            NullLogger<SyncService>.Instance);

    private static Appointment Appt(string id, DateTimeOffset date) =>
        new() { Id = id, Date = date, PatientName = "P", Status = "Booked" };

    [Fact]
    public async Task Sync_orders_by_date_and_reports_count()
    {
        var unsorted = new[]
        {
            Appt("b", Now.AddHours(2)),
            Appt("a", Now.AddHours(1))
        };
        var store = new InMemoryStore();
        var service = Create(new FakeDentallyClient(unsorted), store);

        var result = await service.SyncAsync(new SyncRequest { Range = DateRangePreset.Today }, CancellationToken.None);

        Assert.Equal(2, result.RecordCount);
        Assert.Equal("a", result.Appointments[0].Id);
        Assert.Equal(Now, result.SyncedAt);
        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    public async Task Sync_prefers_request_token_over_config()
    {
        var client = new FakeDentallyClient(Array.Empty<Appointment>());
        var service = Create(client, new InMemoryStore());

        await service.SyncAsync(new SyncRequest { Token = "req-token", Range = DateRangePreset.Today }, CancellationToken.None);

        Assert.Equal("req-token", client.LastToken);
    }

    [Fact]
    public async Task Sync_falls_back_to_configured_token()
    {
        var client = new FakeDentallyClient(Array.Empty<Appointment>());
        var service = Create(client, new InMemoryStore());

        await service.SyncAsync(new SyncRequest { Range = DateRangePreset.Today }, CancellationToken.None);

        Assert.Equal("config-token", client.LastToken);
    }

    [Fact]
    public async Task GetAppointment_returns_the_matching_cached_record()
    {
        var store = new InMemoryStore();
        var service = Create(new FakeDentallyClient(new[] { Appt("a", Now), Appt("b", Now.AddHours(1)) }), store);
        await service.SyncAsync(new SyncRequest { Range = DateRangePreset.Today }, CancellationToken.None);

        var found = await service.GetAppointmentAsync("b", CancellationToken.None);
        var missing = await service.GetAppointmentAsync("zzz", CancellationToken.None);

        Assert.Equal("b", found?.Id);
        Assert.Null(missing);
    }

    [Fact]
    public async Task GetAppointmentDetail_derives_finish_time_from_cache_when_no_live_data()
    {
        var store = new InMemoryStore();
        var client = new FakeDentallyClient(new[] { new Appointment { Id = "a", Date = Now, Duration = 30, PatientName = "P", PractitionerName = "Dr", Status = "Booked" } });
        var service = Create(client, store);
        await service.SyncAsync(new SyncRequest { Range = DateRangePreset.Today }, CancellationToken.None);

        var detail = await service.GetAppointmentDetailAsync("a", CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal(Now.AddMinutes(30), detail!.FinishTime);
        Assert.Equal("Dr", detail.PractitionerName);
        Assert.Null(detail.Patient);
    }

    [Fact]
    public async Task GetAppointmentDetail_merges_live_extras_over_cache()
    {
        var store = new InMemoryStore();
        var client = new FakeDentallyClient(new[] { new Appointment { Id = "a", Date = Now, Duration = 30, PatientName = "Cached Name", PractitionerName = "Dr", Status = "Booked" } })
        {
            DetailToReturn = new AppointmentDetail
            {
                Id = "a", Date = Now, Duration = 30,
                Notes = "Bring x-rays",
                Patient = new PatientContact { Id = "55", Email = "p@example.com", Phone = "0789", DateOfBirth = "1990-01-01" }
            }
        };
        var service = Create(client, store);
        await service.SyncAsync(new SyncRequest { Range = DateRangePreset.Today }, CancellationToken.None);

        var detail = await service.GetAppointmentDetailAsync("a", CancellationToken.None);

        Assert.Equal("Bring x-rays", detail!.Notes);
        Assert.Equal("p@example.com", detail.Patient?.Email);
        Assert.Equal("Cached Name", detail.PatientName); // cache name preferred
    }

    [Fact]
    public async Task GetAppointmentDetail_returns_null_when_not_cached_and_no_live()
    {
        var service = Create(new FakeDentallyClient(Array.Empty<Appointment>()), new InMemoryStore());
        Assert.Null(await service.GetAppointmentDetailAsync("missing", CancellationToken.None));
    }

    [Fact]
    public async Task Sync_does_not_save_when_fetch_fails()
    {
        var store = new InMemoryStore();
        var service = Create(new FakeDentallyClient(new DentallyApiException("boom", 502)), store);

        await Assert.ThrowsAsync<DentallyApiException>(() =>
            service.SyncAsync(new SyncRequest { Range = DateRangePreset.Today }, CancellationToken.None));

        Assert.Equal(0, store.SaveCount);
        Assert.Null(store.Saved);
    }
}
