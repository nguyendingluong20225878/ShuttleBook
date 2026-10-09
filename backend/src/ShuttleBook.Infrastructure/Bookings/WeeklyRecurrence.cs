namespace ShuttleBook.Infrastructure.Bookings;

public sealed record WeeklyOccurrence(DateOnly Date, TimeOnly LocalStart, TimeOnly LocalEnd,
    DateTimeOffset StartsAt, DateTimeOffset EndsAt);

/// <summary>Calendar recurrence only. The API checks publication, horizon, current time, price and allocation.</summary>
public static class WeeklyRecurrence
{
    public static bool TryGenerate(DateOnly startsOn, DateOnly endsOn, DayOfWeek dayOfWeek,
        TimeOnly localStart, int durationMinutes, int courtMinimumMinutes, int maximumOccurrences,
        TimeZoneInfo zone, out IReadOnlyList<WeeklyOccurrence> occurrences, out string? error)
    {
        occurrences = []; error = "VALIDATION_FAILED";
        if (!Enum.IsDefined(dayOfWeek) || durationMinutes < Math.Max(120, courtMinimumMinutes) ||
            durationMinutes % 30 != 0 || maximumOccurrences < 1 ||
            localStart.Ticks % TimeSpan.FromMinutes(30).Ticks != 0 || endsOn < startsOn)
            return false;
        DateOnly minimumEnd;
        try { minimumEnd = startsOn.AddMonths(1); }
        catch (ArgumentOutOfRangeException) { return false; }
        if (endsOn < minimumEnd || durationMinutes >= 1440 ||
            localStart.ToTimeSpan().TotalMinutes + durationMinutes >= 1440) return false;
        var localEnd = localStart.AddMinutes(durationMinutes);
        var daysToFirst = ((int)dayOfWeek - (int)startsOn.DayOfWeek + 7) % 7;
        var firstDayNumber = startsOn.DayNumber + daysToFirst;
        if (firstDayNumber > endsOn.DayNumber) return false;
        var count = (endsOn.DayNumber - firstDayNumber) / 7 + 1;
        if (count > maximumOccurrences) return false;
        var values = new List<WeeklyOccurrence>(count);
        for (var dayNumber = firstDayNumber; dayNumber <= endsOn.DayNumber; dayNumber += 7)
        {
            var date = DateOnly.FromDayNumber(dayNumber);
            var start = date.ToDateTime(localStart, DateTimeKind.Unspecified);
            var end = date.ToDateTime(localEnd, DateTimeKind.Unspecified);
            for (var cursor = start; cursor <= end; cursor = cursor.AddMinutes(30))
                if (zone.IsInvalidTime(cursor) || zone.IsAmbiguousTime(cursor))
                { error = "SCHEDULE_UNAVAILABLE"; return false; }
            try
            {
                values.Add(new(date, localStart, localEnd,
                    new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(start, zone), TimeSpan.Zero),
                    new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(end, zone), TimeSpan.Zero)));
            }
            catch (ArgumentException) { error = "SCHEDULE_UNAVAILABLE"; return false; }
        }
        occurrences = values; error = null;
        return true;
    }
}
