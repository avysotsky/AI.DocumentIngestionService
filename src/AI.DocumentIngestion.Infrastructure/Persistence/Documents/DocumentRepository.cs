using AI.DocumentIngestion.Application.Abstractions;
using AI.DocumentIngestion.Domain.Documents;
using Microsoft.EntityFrameworkCore;

namespace AI.DocumentIngestion.Infrastructure.Persistence.Documents;

internal sealed class DocumentRepository : IDocumentRepository
{
    private readonly DocumentIngestionDbContext _dbContext;

    public DocumentRepository(DocumentIngestionDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(Document document, CancellationToken cancellationToken)
    {
        await _dbContext.Documents.AddAsync(document, cancellationToken);
    }

    public Task<Document?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.Documents
            .AsNoTracking()
            .SingleOrDefaultAsync(document => document.Id == id, cancellationToken);
    }

    public Task<Document?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.Documents
            .SingleOrDefaultAsync(document => document.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Document>> ListAsync(
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Documents
            .AsNoTracking()
            .OrderByDescending(document => document.CreatedAt)
            .ThenBy(document => document.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountAsync(CancellationToken cancellationToken)
    {
        return _dbContext.Documents.CountAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
