using AI.DocumentIngestion.Application.Abstractions;

namespace AI.DocumentIngestion.Application.Documents;

public sealed class ReprocessDocumentHandler
{
    private readonly IDocumentRepository _documents;
    private readonly IDocumentEventPublisher _events;
    private readonly IClock _clock;

    public ReprocessDocumentHandler(
        IDocumentRepository documents,
        IDocumentEventPublisher events,
        IClock clock)
    {
        _documents = documents;
        _events = events;
        _clock = clock;
    }

    public async Task<DocumentDto?> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var document = await _documents.GetForUpdateAsync(id, cancellationToken);
        if (document is null)
        {
            return null;
        }

        document.QueueForReprocessing();
        await _events.EnqueueUploadedAsync(document.Id, _clock.UtcNow, cancellationToken);
        await _documents.SaveChangesAsync(cancellationToken);

        return DocumentDto.FromDomain(document);
    }
}
