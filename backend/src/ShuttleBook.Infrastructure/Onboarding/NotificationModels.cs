namespace ShuttleBook.Infrastructure.Onboarding;

public sealed class OutboxMessage
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string EventType { get; set; } = "";
    public Guid? TargetUserId { get; set; }
    public Guid EntityId { get; set; }
    public string Payload { get; set; } = "{}";
    public int Attempts { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class Notification
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid OutboxMessageId { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
}
