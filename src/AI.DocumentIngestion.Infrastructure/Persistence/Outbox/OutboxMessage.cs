namespace AI.DocumentIngestion.Infrastructure.Persistence.Outbox;

public sealed class OutboxMessage
{
    private OutboxMessage()
    {
    }

    private OutboxMessage(Guid id, DateTimeOffset occurredAt, string type, string payload)
    {
        Id = id;
        OccurredAt = occurredAt;
        Type = type;
        Payload = payload;
    }

    public Guid Id { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public DateTimeOffset? ProcessedAt { get; private set; }
    public int AttemptCount { get; private set; }
    public string? LastError { get; private set; }

    public static OutboxMessage Create(
        Guid id,
        DateTimeOffset occurredAt,
        string type,
        string payload)
    {
        return new OutboxMessage(id, occurredAt, type, payload);
    }
}
