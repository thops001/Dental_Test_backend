namespace dental_backend.Dentally;

public sealed class DentallyOptions
{
    public const string SectionName = "Dentally";

    public string BaseUrl { get; set; } = "https://api.dentally.co/v1/";

    /// <summary>Fallback token used when a request does not supply one.</summary>
    public string? Token { get; set; }

    /// <summary>User-Agent sent with every request — Dentally rejects requests without one.</summary>
    public string UserAgent { get; set; } = "DentPulse/1.0";

    /// <summary>Page size requested from Dentally (max 100).</summary>
    public int PageSize { get; set; } = 100;

    /// <summary>Maximum number of pages fetched in parallel.</summary>
    public int MaxConcurrency { get; set; } = 5;

    /// <summary>When true, look up missing patient names via GET /patients/{id} during a sync.</summary>
    public bool EnrichPatientNames { get; set; } = true;

    /// <summary>When true, serve generated data instead of calling the live API (see README).</summary>
    public bool UseMock { get; set; }
}
