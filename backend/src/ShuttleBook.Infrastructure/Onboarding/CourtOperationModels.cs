namespace ShuttleBook.Infrastructure.Onboarding;

public sealed class CourtAllocation
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid CourtId { get; set; }
    public string Kind { get; set; } = "MAINTENANCE";
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public string Status { get; set; } = "RESERVED";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReleasedAt { get; set; }
}

public sealed class CourtMaintenance
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid CourtId { get; set; }
    public Guid AllocationId { get; set; }
    public string Reason { get; set; } = "";
    public string Status { get; set; } = "ACTIVE";
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CancelledBy { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
}
