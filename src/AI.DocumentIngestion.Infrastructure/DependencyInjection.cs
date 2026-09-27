using AI.DocumentIngestion.Application.Abstractions;
using AI.DocumentIngestion.Infrastructure.Persistence;
using AI.DocumentIngestion.Infrastructure.Persistence.Documents;
using AI.DocumentIngestion.Infrastructure.Messaging;
using AI.DocumentIngestion.Infrastructure.Embeddings;
using AI.DocumentIngestion.Infrastructure.Persistence.Outbox;
using AI.DocumentIngestion.Infrastructure.Storage;
using AI.DocumentIngestion.Infrastructure.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pgvector.EntityFrameworkCore;

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
            options.UseNpgsql(connectionString, npgsql => npgsql.UseVector()));
        var objectStorageRoot = configuration[
            $"{FileObjectStorageOptions.SectionName}:RootPath"] ?? "data/objects";
        services.Configure<FileObjectStorageOptions>(options =>
            options.RootPath = objectStorageRoot);

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IObjectStorage, FileObjectStorage>();
        services.AddOptions<PdfTextExtractionOptions>()
            .Configure(options =>
            {
                var section = configuration.GetSection(PdfTextExtractionOptions.SectionName);
                options.MaximumPages = int.TryParse(
                    section[nameof(PdfTextExtractionOptions.MaximumPages)],
                    out var maximumPages)
                    ? maximumPages
                    : options.MaximumPages;
                options.MaximumExtractedCharacters = int.TryParse(
                    section[nameof(PdfTextExtractionOptions.MaximumExtractedCharacters)],
                    out var maximumExtractedCharacters)
                    ? maximumExtractedCharacters
                    : options.MaximumExtractedCharacters;
            })
            .Validate(
                options => options.MaximumPages is > 0 and <= 10_000,
                "PDF maximum page count must be between 1 and 10000.")
            .Validate(
                options => options.MaximumExtractedCharacters is >= 1_000 and <= 50_000_000,
                "PDF maximum extracted character count must be between 1000 and 50000000.")
            .ValidateOnStart();
        services.AddSingleton<PdfTextExtractor>();
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IDocumentEventPublisher, OutboxDocumentEventPublisher>();
        services.AddOptions<RabbitMqOptions>()
            .Configure(options =>
            {
                var section = configuration.GetSection(RabbitMqOptions.SectionName);
                options.HostName = section[nameof(RabbitMqOptions.HostName)] ?? string.Empty;
                options.Port = int.TryParse(
                    section[nameof(RabbitMqOptions.Port)],
                    out var port) ? port : 5672;
                options.VirtualHost = section[nameof(RabbitMqOptions.VirtualHost)] ?? "/";
                options.UserName = section[nameof(RabbitMqOptions.UserName)] ?? string.Empty;
                options.Password = section[nameof(RabbitMqOptions.Password)] ?? string.Empty;
                options.ExchangeName = section[nameof(RabbitMqOptions.ExchangeName)] ?? options.ExchangeName;
                options.QueueName = section[nameof(RabbitMqOptions.QueueName)] ?? options.QueueName;
                options.RoutingKey = section[nameof(RabbitMqOptions.RoutingKey)] ?? options.RoutingKey;
            })
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.HostName) &&
                           options.Port is > 0 and <= 65535 &&
                           !string.IsNullOrWhiteSpace(options.VirtualHost) &&
                           !string.IsNullOrWhiteSpace(options.UserName) &&
                           !string.IsNullOrWhiteSpace(options.Password),
                "RabbitMq endpoint and credentials are required in external configuration.")
            .ValidateOnStart();
        services.AddSingleton<RabbitMqConnection>();
        services.AddHostedService<OutboxPublisherService>();

        return services;
    }

    public static IServiceCollection AddOnnxEmbeddings(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<OnnxEmbeddingOptions>()
            .Configure(options =>
            {
                var section = configuration.GetSection(OnnxEmbeddingOptions.SectionName);
                options.ModelPath = section[nameof(OnnxEmbeddingOptions.ModelPath)] ?? string.Empty;
                options.BatchSize = int.TryParse(
                    section[nameof(OnnxEmbeddingOptions.BatchSize)], out var batchSize)
                    ? batchSize
                    : 8;
                options.MaxTokenLength = int.TryParse(
                    section[nameof(OnnxEmbeddingOptions.MaxTokenLength)], out var maxTokenLength)
                    ? maxTokenLength
                    : 512;
            })
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ModelPath),
                "Embedding model path is required.")
            .Validate(
                options => options.BatchSize is > 0 and <= 128,
                "Embedding batch size must be between 1 and 128.")
            .Validate(
                options => options.MaxTokenLength is >= 8 and <= 512,
                "Maximum token length must be between 8 and 512.")
            .ValidateOnStart();
        services.AddSingleton<IEmbeddingProvider, OnnxEmbeddingProvider>();
        return services;
    }
}
