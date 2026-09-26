using AI.DocumentIngestion.Application.Abstractions;

namespace AI.DocumentIngestion.Application.Documents;

public sealed record ListDocumentsQuery(int Page = 1, int PageSize = 20);

public sealed record DocumentPage(
    IReadOnlyList<DocumentDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed class ListDocumentsHandler
{
    public const int MaximumPageSize = 100;

    private readonly IDocumentRepository _documents;

    public ListDocumentsHandler(IDocumentRepository documents)
    {
        _documents = documents;
    }

    public async Task<DocumentPage> HandleAsync(
        ListDocumentsQuery query,
        CancellationToken cancellationToken)
    {
        if (query.Page <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(query), "Page must be positive.");
        }

        if (query.PageSize is <= 0 or > MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query),
                $"Page size must be between 1 and {MaximumPageSize}.");
        }

        var skip = checked((query.Page - 1) * query.PageSize);
        var documents = await _documents.ListAsync(skip, query.PageSize, cancellationToken);
        var totalCount = await _documents.CountAsync(cancellationToken);

        return new DocumentPage(
            documents.Select(DocumentDto.FromDomain).ToArray(),
            query.Page,
            query.PageSize,
            totalCount);
    }
}
