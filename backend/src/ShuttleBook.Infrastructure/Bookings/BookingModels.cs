namespace ShuttleBook.Infrastructure.Bookings;

public sealed class BookingQuote
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid CourtId { get; set; }
    public Guid VenueId { get; set; }
    public DateOnly LocalDate { get; set; }
    public TimeOnly LocalStart { get; set; }
    public TimeOnly LocalEnd { get; set; }
    public string Timezone { get; set; } = "";
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public long Amount { get; set; }
    public string Slots { get; set; } = "[]";
    public string Fingerprint { get; set; } = "";
    public int BookingBlockMinutes { get; set; }
    public int MinimumBookingMinutes { get; set; }
    public int HoldMinutes { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class Booking
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string BookingNo { get; set; } = "";
    public Guid CustomerId { get; set; }
    public Guid VenueId { get; set; }
    public Guid CourtId { get; set; }
    public Guid AllocationId { get; set; }
    public string BookingType { get; set; } = "CASUAL";
    public string Status { get; set; } = "AWAITING_TRANSFER";
    public string VenueName { get; set; } = "";
    public string CourtName { get; set; } = "";
    public string Timezone { get; set; } = "";
    public DateOnly LocalDate { get; set; }
    public TimeOnly LocalStart { get; set; }
    public TimeOnly LocalEnd { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public long Amount { get; set; }
    public string Slots { get; set; } = "[]";
    public int BookingBlockMinutes { get; set; }
    public int MinimumBookingMinutes { get; set; }
    public int HoldMinutes { get; set; }
    public DateTimeOffset PaymentDeadline { get; set; }
    public long Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ExpiredAt { get; set; }
}

public sealed class BookingPayment
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid BookingId { get; set; }
    public string Status { get; set; } = "AWAITING_TRANSFER";
    public long ExpectedAmount { get; set; }
    public string RecipientSnapshot { get; set; } = "{}";
    public Guid QrUploadId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? FirstReportedAt { get; set; }
    public DateTimeOffset? LastReportedAt { get; set; }
    public long? ConfirmedAmount { get; set; }
    public Guid? ConfirmedBy { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public DateTimeOffset? ConfirmationAlertedAt { get; set; }
}

public sealed class BookingIdempotency
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid ActorUserId { get; set; }
    public string Operation { get; set; } = "CASUAL_CREATE";
    public string Key { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public Guid BookingId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class PaymentEvidence
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid PaymentId { get; set; }
    public Guid BookingId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid VenueId { get; set; }
    public Guid? ProofUploadId { get; set; }
    public string Kind { get; set; } = "INITIAL";
    public string? BankReference { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset ReportedAt { get; set; }
}

public sealed class PaymentDecision
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid PaymentId { get; set; }
    public Guid BookingId { get; set; }
    public Guid ActorUserId { get; set; }
    public string Resolution { get; set; } = "";
    public string? ReasonCode { get; set; }
    public string? Reason { get; set; }
    public long? ConfirmedAmount { get; set; }
    public string? BankReference { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset DecidedAt { get; set; }
}
