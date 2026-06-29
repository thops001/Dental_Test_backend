using dental_backend.Models;

namespace dental_backend.Tests;

public class DateRangeTests
{
    // 2026-06-24 is a Wednesday.
    private static readonly DateTimeOffset Wed = new(2026, 6, 24, 13, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Today_uses_the_reference_day()
    {
        var range = DateRange.Resolve(DateRangePreset.Today, null, null, Wed);
        Assert.Equal(new DateOnly(2026, 6, 24), range.From);
        Assert.Equal(new DateOnly(2026, 6, 24), range.To);
    }

    [Fact]
    public void ThisWeek_spans_Monday_to_Sunday()
    {
        var range = DateRange.Resolve(DateRangePreset.ThisWeek, null, null, Wed);
        Assert.Equal(new DateOnly(2026, 6, 22), range.From); // Monday
        Assert.Equal(new DateOnly(2026, 6, 28), range.To);   // Sunday
    }

    [Fact]
    public void Custom_returns_the_supplied_bounds()
    {
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 1, 31);
        var range = DateRange.Resolve(DateRangePreset.Custom, from, to, Wed);
        Assert.Equal(from, range.From);
        Assert.Equal(to, range.To);
    }

    [Fact]
    public void Custom_without_bounds_throws()
    {
        Assert.Throws<ArgumentException>(() =>
            DateRange.Resolve(DateRangePreset.Custom, null, null, Wed));
    }

    [Fact]
    public void Custom_with_inverted_bounds_throws()
    {
        Assert.Throws<ArgumentException>(() =>
            DateRange.Resolve(DateRangePreset.Custom, new DateOnly(2026, 2, 1), new DateOnly(2026, 1, 1), Wed));
    }
}
