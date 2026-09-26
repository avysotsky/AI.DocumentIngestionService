using AI.DocumentIngestion.Domain.Documents;

namespace AI.DocumentIngestion.Application.Abstractions;

public interface IDocumentRepository
{
    Task AddAsync(Document document, CancellationToken cancellationToken);

    Task<Document?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<Document?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Document>> ListAsync(
        int skip,
        int take,
        CancellationToken cancellationToken);

    Task<int> CountAsync(CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
