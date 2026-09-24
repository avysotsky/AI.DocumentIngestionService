using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AI.DocumentIngestion.Infrastructure.Persistence;

public sealed class DocumentIngestionDbContextFactory
    : IDesignTimeDbContextFactory<DocumentIngestionDbContext>
{
    public DocumentIngestionDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<DocumentIngestionDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5432;Database=document_ingestion;Username=document_ingestion")
            .Options;

        return new DocumentIngestionDbContext(options);
    }
}
