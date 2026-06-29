using dental_backend.Models;
using dental_backend.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace dental_backend.Tests;

public class JsonAppointmentStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "dentpulse-tests", Guid.NewGuid().ToString());

    private JsonAppointmentStore CreateStore()
    {
        var options = Options.Create(new JsonStoreOptions { FilePath = Path.Combine(_dir, "appointments.json") });
        var env = new TestHostEnvironment { ContentRootPath = _dir };
        return new JsonAppointmentStore(options, env);
    }

    private static SyncResult Sample(int count) => new()
    {
        RecordCount = count,
        DurationMs = 12.3,
        SyncedAt = new DateTimeOffset(2026, 6, 24, 9, 0, 0, TimeSpan.Zero),
        From = new DateOnly(2026, 6, 24),
        To = new DateOnly(2026, 6, 24),
        Appointments = Enumerable.Range(0, count).Select(i => new Appointment
        {
            Id = i.ToString(),
            Date = new DateTimeOffset(2026, 6, 24, 9, 0, 0, TimeSpan.Zero),
            PatientName = $"Patient {i}",
            Status = "Booked"
        }).ToList()
    };

    [Fact]
    public async Task Read_returns_null_before_any_save()
    {
        var store = CreateStore();
        Assert.Null(await store.ReadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Save_then_read_round_trips()
    {
        var store = CreateStore();
        await store.SaveAsync(Sample(3), CancellationToken.None);

        var read = await store.ReadAsync(CancellationToken.None);
        Assert.NotNull(read);
        Assert.Equal(3, read!.RecordCount);
        Assert.Equal(3, read.Appointments.Count);
        Assert.Equal("Patient 0", read.Appointments[0].PatientName);
    }

    [Fact]
    public async Task Save_overwrites_previous_cache()
    {
        var store = CreateStore();
        await store.SaveAsync(Sample(5), CancellationToken.None);
        await store.SaveAsync(Sample(2), CancellationToken.None);

        var read = await store.ReadAsync(CancellationToken.None);
        Assert.Equal(2, read!.Appointments.Count);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string ApplicationName { get; set; } = "tests";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
