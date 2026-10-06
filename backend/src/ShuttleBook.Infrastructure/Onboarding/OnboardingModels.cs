namespace ShuttleBook.Infrastructure.Onboarding;

public sealed class Business
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Name { get; set; } = "";
    public string LegalName { get; set; } = "";
    public string Contact { get; set; } = "";
    public string Status { get; set; } = "DRAFT";
    public long Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class BusinessMembership
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid BusinessId { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; } = "OWNER";
    public string Status { get; set; } = "PENDING";
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class Venue
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid BusinessId { get; set; }
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string Contact { get; set; } = "";
    public string Timezone { get; set; } = "Asia/Ho_Chi_Minh";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Status { get; set; } = "DRAFT";
    public Guid? ImageUploadId { get; set; }
    public long Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class Court
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid VenueId { get; set; }
    public string Name { get; set; } = "";
    public string Status { get; set; } = "INACTIVE";
    public long Version { get; set; } = 1;
    public int BookingBlockMinutes { get; set; } = 30;
    public int MinimumBookingMinutes { get; set; } = 30;
    public int HoldMinutes { get; set; } = 20;
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class CourtOperatingHour
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid CourtId { get; set; }
    public int DayOfWeek { get; set; }
    public TimeOnly OpensAt { get; set; }
    public TimeOnly ClosesAt { get; set; }
}

public sealed class PricingRule
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid CourtId { get; set; }
    public int DayOfWeek { get; set; }
    public DateOnly StartsOn { get; set; } = DateOnly.MinValue;
    public DateOnly EndsOn { get; set; } = DateOnly.MaxValue;
    public TimeOnly StartsAt { get; set; }
    public TimeOnly EndsAt { get; set; }
    public long PricePerSlot { get; set; }
    public int Priority { get; set; }
}

public sealed class VenuePaymentAccount
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid VenueId { get; set; }
    public string BankCode { get; set; } = "";
    public string AccountName { get; set; } = "";
    public string AccountNumber { get; set; } = "";
    public Guid QrUploadId { get; set; }
}

public sealed class MediaUpload
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid OwnerUserId { get; set; }
    public Guid VenueId { get; set; }
    public string Purpose { get; set; } = "";
    public string ObjectKey { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public string Sha256Base64 { get; set; } = "";
    public string Status { get; set; } = "PENDING";
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ApprovalRequest
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid BusinessId { get; set; }
    public Guid? VenueId { get; set; }
    public string Kind { get; set; } = "ONBOARDING";
    public Guid SubmittedBy { get; set; }
    public Guid? ReviewedBy { get; set; }
    public string Status { get; set; } = "PENDING";
    public string Snapshot { get; set; } = "{}";
    public string? Reason { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
}
