namespace ShuttleBook.Infrastructure.Onboarding;

public static class CourtPricing
{
    public static PricingRule? Select(IEnumerable<PricingRule> rules, TimeOnly start, TimeOnly end) =>
        rules.Where(x => x.StartsAt <= start && x.EndsAt >= end).OrderByDescending(x => x.Priority).FirstOrDefault();
}
