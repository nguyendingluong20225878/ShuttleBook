using ShuttleBook.Infrastructure.Bookings;

namespace ShuttleBook.Api.Tests;

public sealed class WeeklyRecurrenceTests
{
    [Theory]
    [InlineData("2026-04-01", "2026-05-01", DayOfWeek.Tuesday, 4)]
    [InlineData("2026-10-01", "2026-11-01", DayOfWeek.Thursday, 5)]
    [InlineData("2027-01-31", "2027-02-28", DayOfWeek.Sunday, 5)]
    public void Generates_inclusive_calendar_month_and_same_weekday(string from, string to, DayOfWeek weekday, int count)
    {
        Assert.True(Generate(from, to, weekday, 120, out var values, out var error));
        Assert.Null(error); Assert.Equal(count, values.Count);
        Assert.All(values, occurrence =>
        {
            Assert.Equal(weekday, occurrence.Date.DayOfWeek);
            Assert.InRange(occurrence.Date, DateOnly.Parse(from), DateOnly.Parse(to));
            Assert.Equal(new TimeOnly(18, 0), occurrence.LocalStart);
            Assert.Equal(new TimeOnly(20, 0), occurrence.LocalEnd);
            Assert.Equal(TimeSpan.FromHours(2), occurrence.EndsAt - occurrence.StartsAt);
        });
    }

    [Theory]
    [InlineData("2026-10-01", "2026-10-29", 120)]
    [InlineData("2026-10-01", "2026-11-01", 90)]
    [InlineData("2026-10-01", "2026-11-01", 135)]
    [InlineData("2026-10-01", "2026-11-01", 390)]
    public void Rejects_short_month_short_non_grid_or_overnight_duration(string from, string to, int duration)
    {
        Assert.False(Generate(from, to, DayOfWeek.Thursday, duration, out var values, out var error));
        Assert.Empty(values); Assert.Equal("VALIDATION_FAILED", error);
    }

    [Fact]
    public void Supports_half_hour_extensions_and_respects_court_minimum_and_occurrence_limit()
    {
        Assert.True(Generate("2026-10-01", "2026-11-01", DayOfWeek.Thursday, 150, out var values, out _));
        Assert.Equal(TimeSpan.FromMinutes(150), values[0].EndsAt - values[0].StartsAt);
        Assert.False(WeeklyRecurrence.TryGenerate(new(2026, 10, 1), new(2026, 11, 1), DayOfWeek.Thursday,
            new(18, 0), 120, 180, 12, TimeZoneInfo.Utc, out _, out _));
        Assert.False(WeeklyRecurrence.TryGenerate(new(2026, 10, 1), new(2026, 11, 1), DayOfWeek.Thursday,
            new(18, 0), 120, 30, 4, TimeZoneInfo.Utc, out _, out _));
        Assert.False(WeeklyRecurrence.TryGenerate(new(2026, 10, 1), new(2026, 11, 1), DayOfWeek.Thursday,
            new(18, 15), 120, 30, 12, TimeZoneInfo.Utc, out _, out _));
    }

    [Theory]
    [InlineData("2026-03-01", "2026-04-01", 1, 30)]
    [InlineData("2026-10-25", "2026-11-25", 0, 30)]
    public void Rejects_any_invalid_or_ambiguous_half_hour_boundary(string from, string to, int hour, int minute)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        Assert.False(WeeklyRecurrence.TryGenerate(DateOnly.Parse(from), DateOnly.Parse(to), DayOfWeek.Sunday,
            new(hour, minute), 120, 30, 12, zone, out var values, out var error));
        Assert.Empty(values); Assert.Equal("SCHEDULE_UNAVAILABLE", error);
    }

    [Fact]
    public void Preserves_local_grid_with_non_hour_utc_offset()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kathmandu");
        Assert.True(WeeklyRecurrence.TryGenerate(new(2026, 10, 1), new(2026, 11, 1), DayOfWeek.Thursday,
            new(18, 0), 120, 30, 12, zone, out var values, out _));
        Assert.Equal(15, values[0].StartsAt.Minute);
        Assert.Equal(TimeSpan.Zero, values[0].StartsAt.Offset);
        Assert.Equal(new TimeOnly(18, 0), values[0].LocalStart);
    }

    private static bool Generate(string from, string to, DayOfWeek weekday, int duration,
        out IReadOnlyList<WeeklyOccurrence> values, out string? error) =>
        WeeklyRecurrence.TryGenerate(DateOnly.Parse(from), DateOnly.Parse(to), weekday, new(18, 0), duration,
            30, 12, TimeZoneInfo.Utc, out values, out error);
}
