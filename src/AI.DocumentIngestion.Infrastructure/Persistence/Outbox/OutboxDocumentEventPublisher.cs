using System.Text.Json;
using AI.DocumentIngestion.Application.Abstractions;
using AI.DocumentIngestion.Application.Events;

namespace AI.DocumentIngestion.Infrastructure.Persistence.Outbox;

internal sealed class OutboxDocumentEventPublisher : IDocumentEventPublisher
{
    private readonly DocumentIngestionDbContext _dbContext;

    public OutboxDocumentEventPublisher(DocumentIngestionDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task EnqueueUploadedAsync(
        Guid documentId,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        var messageId = Guid.NewGuid();
        var documentUploaded = new DocumentUploadedV1(messageId, occurredAt, documentId);
        var outboxMessage = OutboxMessage.Create(
            messageId,
            occurredAt,
            nameof(DocumentUploadedV1),
            JsonSerializer.Serialize(documentUploaded));

        await _dbContext.OutboxMessages.AddAsync(outboxMessage, cancellationToken);
    }
}
