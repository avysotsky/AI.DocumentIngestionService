using AI.DocumentIngestion.Application.Abstractions;

namespace AI.DocumentIngestion.Application.Documents;

public sealed class GetDocumentHandler
{
    private readonly IDocumentRepository _documents;

    public GetDocumentHandler(IDocumentRepository documents)
    {
        _documents = documents;
    }

    public async Task<DocumentDto?> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var document = await _documents.GetAsync(id, cancellationToken);
        return document is null ? null : DocumentDto.FromDomain(document);
    }
}
