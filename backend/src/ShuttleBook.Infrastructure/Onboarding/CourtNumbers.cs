using System.Globalization;

namespace ShuttleBook.Infrastructure.Onboarding;

public static class CourtNumbers
{
    public static bool TryParse(string? name, out int number)
    {
        number = 0;
        if (name is null) return false;
        var value = name.Trim();
        if (value.StartsWith("Sân ", StringComparison.OrdinalIgnoreCase)) value = value[4..].Trim();
        return value.Length is > 0 and <= 100 && value.All(c => c is >= '0' and <= '9') &&
            int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out number) && number > 0;
    }

    public static string Name(int number) => $"Sân {number.ToString(CultureInfo.InvariantCulture)}";

    // Legacy names remain readable. Numbered courts precede legacy names and sort numerically.
    public static IOrderedEnumerable<Court> Order(IEnumerable<Court> courts) => courts
        .OrderBy(c => TryParse(c.Name, out _) ? 0 : 1)
        .ThenBy(c => TryParse(c.Name, out var number) ? number : 0)
        .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
        .ThenBy(c => c.Id);
}
