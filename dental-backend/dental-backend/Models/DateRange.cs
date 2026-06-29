namespace dental_backend.Models;

public enum DateRangePreset
{
    Today,
    ThisWeek,
    Custom
}

/// <summary>
/// A resolved, inclusive-from / exclusive-to UTC date range used to query Dentally.
/// </summary>
public readonly record struct DateRange(DateOnly From, DateOnly To)
{
    /// <summary>
    /// Resolves a preset (or explicit custom range) into concrete UTC dates.
    /// "Today" and "This week" are evaluated against the supplied reference instant (UTC).
    /// </summary>
    public static DateRange Resolve(DateRangePreset preset, DateOnly? from, DateOnly? to, DateTimeOffset utcNow)
    {
        var today = DateOnly.FromDateTime(utcNow.UtcDateTime);

        return preset switch
        {
            DateRangePreset.Today => new DateRange(today, today),
            DateRangePreset.ThisWeek => ThisWeek(today),
            DateRangePreset.Custom => ResolveCustom(from, to),
            _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unknown date range preset.")
        };
    }

    private static DateRange ThisWeek(DateOnly today)
    {
        // ISO week: Monday start.
        int diff = ((int)today.DayOfWeek + 6) % 7;
        var monday = today.AddDays(-diff);
        return new DateRange(monday, monday.AddDays(6));
    }

    private static DateRange ResolveCustom(DateOnly? from, DateOnly? to)
    {
        if (from is null || to is null)
            throw new ArgumentException("A custom date range requires both 'from' and 'to'.");
        if (to < from)
            throw new ArgumentException("'to' must not be earlier than 'from'.");
        return new DateRange(from.Value, to.Value);
    }
}
