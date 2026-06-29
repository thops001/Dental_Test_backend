using System.Text.Json;
using dental_backend.Models;
using Microsoft.Extensions.Options;

namespace dental_backend.Storage;

public sealed class JsonStoreOptions
{
    public const string SectionName = "Storage";

    /// <summary>Path to the JSON cache file. Relative paths resolve against the content root.</summary>
    public string FilePath { get; set; } = "data/appointments.json";
}

/// <summary>
/// Persists the latest sync to a single JSON file. Writes go to a temp file and are then
/// atomically moved into place, so a crash mid-write can never leave a half-written cache.
/// A lock serialises concurrent writers within the process.
/// </summary>
public sealed class JsonAppointmentStore : IAppointmentStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _path;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public JsonAppointmentStore(IOptions<JsonStoreOptions> options, IHostEnvironment env)
    {
        var configured = options.Value.FilePath;
        _path = Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(env.ContentRootPath, configured);
    }

    public async Task SaveAsync(SyncResult result, CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(directory);

        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var tmp = _path + ".tmp";
            await using (var stream = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, result, JsonOptions, ct).ConfigureAwait(false);
            }
            File.Move(tmp, _path, overwrite: true);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<SyncResult?> ReadAsync(CancellationToken ct)
    {
        if (!File.Exists(_path))
            return null;

        await using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await JsonSerializer.DeserializeAsync<SyncResult>(stream, JsonOptions, ct).ConfigureAwait(false);
    }
}
