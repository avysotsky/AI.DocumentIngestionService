using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AI.DocumentIngestion.Infrastructure.Persistence;

public sealed class DocumentIngestionDbContextFactory
    : IDesignTimeDbContextFactory<DocumentIngestionDbContext>
{
    public DocumentIngestionDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddDocumentIngestionExternalConfiguration(Directory.GetCurrentDirectory())
            .Build();
        var connectionString = configuration.GetConnectionString("PostgreSql")
            ?? throw new InvalidOperationException("Connection string 'PostgreSql' is required.");

        var options = new DbContextOptionsBuilder<DocumentIngestionDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new DocumentIngestionDbContext(options);
    }
}
