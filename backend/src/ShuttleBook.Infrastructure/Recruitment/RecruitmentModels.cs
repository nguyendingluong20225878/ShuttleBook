namespace ShuttleBook.Infrastructure.Recruitment;

public sealed class RecruitmentPost
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid BookingId { get; set; }
    public Guid OwnerUserId { get; set; }
    public int SlotsNeeded { get; set; }
    public string Level { get; set; } = "ANY";
    public string Caption { get; set; } = "";
    public string ContactName { get; set; } = "";
    public string? ContactPhone { get; set; }
    public bool SharePhoneWithAccepted { get; set; }
    public bool PublishContactPhone { get; set; }
    public string ContributionMode { get; set; } = "UNSPECIFIED";
    public int? CommonContributionVnd { get; set; }
    public int? MaleContributionVnd { get; set; }
    public int? FemaleContributionVnd { get; set; }
    public Guid? ImageUploadId { get; set; }
    public string Status { get; set; } = "OPEN";
    public long Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class RecruitmentRequest
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid PostId { get; set; }
    public Guid ApplicantUserId { get; set; }
    public string Level { get; set; } = "UNSPECIFIED";
    public string Message { get; set; } = "";
    public string? ContactPhone { get; set; }
    public bool SharePhoneWithAccepted { get; set; }
    public string Status { get; set; } = "PENDING";
    public long Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class RecruitmentReport
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid PostId { get; set; }
    public Guid ReporterUserId { get; set; }
    public string Reason { get; set; } = "SPAM";
    public string Status { get; set; } = "OPEN";
    public Guid? ResolvedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
}
