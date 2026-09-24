using AI.DocumentIngestion.Application.Abstractions;
using AI.DocumentIngestion.Infrastructure.Persistence;
using AI.DocumentIngestion.Infrastructure.Persistence.Documents;
using AI.DocumentIngestion.Infrastructure.Persistence.Outbox;
using AI.DocumentIngestion.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AI.DocumentIngestion.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("PostgreSql")
            ?? throw new InvalidOperationException("Connection string 'PostgreSql' is required.");

        services.AddDbContext<DocumentIngestionDbContext>(options =>
            options.UseNpgsql(connectionString));
        var objectStorageRoot = configuration[
            $"{FileObjectStorageOptions.SectionName}:RootPath"] ?? "data/objects";
        services.Configure<FileObjectStorageOptions>(options =>
            options.RootPath = objectStorageRoot);

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IObjectStorage, FileObjectStorage>();
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IDocumentEventPublisher, OutboxDocumentEventPublisher>();

        return services;
    }
}
