using AI.DocumentIngestion.Domain.Documents;
using AI.DocumentIngestion.Infrastructure.Persistence.Inbox;
using AI.DocumentIngestion.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;

namespace AI.DocumentIngestion.Infrastructure.Persistence;

public sealed class DocumentIngestionDbContext : DbContext
{
    public DocumentIngestionDbContext(DbContextOptions<DocumentIngestionDbContext> options)
        : base(options)
    {
    }

    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DocumentIngestionDbContext).Assembly);
    }
}
