namespace AI.DocumentIngestion.Application.Abstractions;

public interface IDocumentEventPublisher
{
    Task EnqueueUploadedAsync(
        Guid documentId,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken);
}
