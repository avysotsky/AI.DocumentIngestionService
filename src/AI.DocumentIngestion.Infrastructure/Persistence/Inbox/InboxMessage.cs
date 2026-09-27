namespace AI.DocumentIngestion.Infrastructure.Persistence.Inbox;

public sealed class InboxMessage
{
    private InboxMessage()
    {
    }

    private InboxMessage(Guid id, DateTimeOffset processedAt)
    {
        Id = id;
        ProcessedAt = processedAt;
    }

    public Guid Id { get; private set; }
    public DateTimeOffset ProcessedAt { get; private set; }

    public static InboxMessage Create(Guid id, DateTimeOffset processedAt)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Message id cannot be empty.", nameof(id));
        }

        return new InboxMessage(id, processedAt);
    }
}
