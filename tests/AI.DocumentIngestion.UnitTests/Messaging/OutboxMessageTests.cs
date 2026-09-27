using AI.DocumentIngestion.Infrastructure.Persistence.Outbox;

namespace AI.DocumentIngestion.UnitTests.Messaging;

public sealed class OutboxMessageTests
{
    [Fact]
    public void RecordFailure_IncrementsAttemptAndPreservesPendingState()
    {
        var message = CreateMessage();

        message.RecordFailure("broker unavailable");

        Assert.Equal(1, message.AttemptCount);
        Assert.Equal("broker unavailable", message.LastError);
        Assert.Null(message.ProcessedAt);
    }

    [Fact]
    public void MarkPublished_CompletesMessageAndClearsFailure()
    {
        var message = CreateMessage();
        var publishedAt = new DateTimeOffset(2026, 9, 27, 8, 0, 0, TimeSpan.Zero);
        message.RecordFailure("temporary failure");

        message.MarkPublished(publishedAt);

        Assert.Equal(publishedAt, message.ProcessedAt);
        Assert.Null(message.LastError);
        Assert.Equal(1, message.AttemptCount);
    }

    private static OutboxMessage CreateMessage()
    {
        return OutboxMessage.Create(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "DocumentUploadedV1",
            "{}");
    }
}
