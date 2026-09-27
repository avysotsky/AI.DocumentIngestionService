using System.Text;
using System.Text.Json;
using AI.DocumentIngestion.Application.Abstractions;
using AI.DocumentIngestion.Application.Events;
using AI.DocumentIngestion.Domain.Documents;
using AI.DocumentIngestion.Infrastructure.Persistence;
using AI.DocumentIngestion.Infrastructure.Persistence.Inbox;
using AI.DocumentIngestion.Infrastructure.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace AI.DocumentIngestion.Infrastructure.Messaging;

public sealed class DocumentUploadedConsumer : BackgroundService
{
    private static readonly Action<ILogger, Exception?> LogMalformedMessage =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(2101, nameof(LogMalformedMessage)),
            "Rejected malformed DocumentUploadedV1 message.");

    private static readonly Action<ILogger, Guid, Exception?> LogProcessingFailure =
        LoggerMessage.Define<Guid>(
            LogLevel.Error,
            new EventId(2102, nameof(LogProcessingFailure)),
            "DocumentUploadedV1 processing failed for message {MessageId}.");

    private readonly RabbitMqConnection _rabbitMq;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DocumentUploadedConsumer> _logger;
    private IChannel? _channel;

    public DocumentUploadedConsumer(
        RabbitMqConnection rabbitMq,
        IServiceScopeFactory scopeFactory,
        ILogger<DocumentUploadedConsumer> logger)
    {
        _rabbitMq = rabbitMq;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connection = await _rabbitMq.GetAsync(stoppingToken);
        _channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
        await RabbitMqTopology.DeclareAsync(_channel, _rabbitMq.Options, stoppingToken);
        await _channel.BasicQosAsync(0, 1, global: false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += HandleDeliveryAsync;
        await _channel.BasicConsumeAsync(
            _rabbitMq.Options.QueueName,
            autoAck: false,
            consumer,
            stoppingToken);

        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }

    private async Task HandleDeliveryAsync(object sender, BasicDeliverEventArgs delivery)
    {
        if (_channel is null)
        {
            return;
        }

        DocumentUploadedV1? message;
        try
        {
            message = JsonSerializer.Deserialize<DocumentUploadedV1>(delivery.Body.Span);
            if (message is null || message.MessageId == Guid.Empty || message.DocumentId == Guid.Empty)
            {
                throw new JsonException("DocumentUploadedV1 is incomplete.");
            }
        }
        catch (JsonException exception)
        {
            LogMalformedMessage(_logger, exception);
            await _channel.BasicRejectAsync(delivery.DeliveryTag, requeue: false);
            return;
        }

        try
        {
            await ProcessAsync(message, delivery.CancellationToken);
            await _channel.BasicAckAsync(delivery.DeliveryTag, multiple: false);
        }
        catch (OperationCanceledException) when (delivery.CancellationToken.IsCancellationRequested)
        {
            await _channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: true);
        }
        catch (Exception exception)
        {
            LogProcessingFailure(_logger, message.MessageId, exception);
            await _channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: true);
        }
    }

    private async Task ProcessAsync(
        DocumentUploadedV1 message,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DocumentIngestionDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var embeddings = scope.ServiceProvider.GetRequiredService<IEmbeddingProvider>();
        var pdfExtractor = scope.ServiceProvider.GetRequiredService<PdfTextExtractor>();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        if (await dbContext.InboxMessages.AnyAsync(
                item => item.Id == message.MessageId,
                cancellationToken))
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        var document = await dbContext.Documents.SingleOrDefaultAsync(
            item => item.Id == message.DocumentId,
            cancellationToken);

        if (document is not null && document.Status == DocumentStatus.Uploaded)
        {
            await ProcessDocumentAsync(
                dbContext,
                storage,
                embeddings,
                pdfExtractor,
                document,
                clock.UtcNow,
                cancellationToken);
        }

        await dbContext.InboxMessages.AddAsync(
            InboxMessage.Create(message.MessageId, clock.UtcNow),
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task ProcessDocumentAsync(
        DocumentIngestionDbContext dbContext,
        IObjectStorage storage,
        IEmbeddingProvider embeddings,
        PdfTextExtractor pdfExtractor,
        Document document,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        document.StartExtraction(now);
        await dbContext.DocumentChunks
            .Where(chunk => chunk.DocumentId == document.Id)
            .ExecuteDeleteAsync(cancellationToken);

        try
        {
            await using var stream = await storage.OpenReadAsync(
                document.StorageKey,
                cancellationToken);
            var chunks = await ExtractAndChunkAsync(
                document.ContentType,
                stream,
                pdfExtractor,
                embeddings,
                cancellationToken);

            document.StartChunking();
            document.StartEmbedding();
            var generatedEmbeddings = await embeddings.GenerateAsync(
                chunks.Select(chunk => chunk.Text).ToArray(),
                EmbeddingInputKind.Passage,
                cancellationToken);
            if (generatedEmbeddings.Count != chunks.Count)
            {
                throw new InvalidOperationException(
                    "Embedding result count does not match chunk count.");
            }

            for (var index = 0; index < chunks.Count; index++)
            {
                await dbContext.DocumentChunks.AddAsync(
                    DocumentChunk.Create(
                        Guid.NewGuid(),
                        document.Id,
                        index,
                        chunks[index].Text,
                        chunks[index].PageNumber,
                        generatedEmbeddings[index].TokenCount,
                        generatedEmbeddings[index].Vector),
                    cancellationToken);
            }

            document.MarkReady(now);
        }
        catch (DocumentExtractionException exception)
        {
            document.MarkFailed(exception.Message);
        }
        catch (DecoderFallbackException)
        {
            document.MarkFailed(
                "The text document is not valid UTF-8, UTF-16, or UTF-32 text.");
        }
    }

    private static async Task<IReadOnlyList<TextChunk>> ExtractAndChunkAsync(
        string contentType,
        Stream stream,
        PdfTextExtractor pdfExtractor,
        IEmbeddingProvider embeddings,
        CancellationToken cancellationToken)
    {
        if (string.Equals(contentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            var pages = await pdfExtractor.ExtractAsync(stream, cancellationToken);
            return TextChunker.ChunkPages(
                pages,
                text => embeddings.CountTokens(text, EmbeddingInputKind.Passage),
                embeddings.MaximumInputTokenCount);
        }

        if (string.Equals(contentType, "text/plain", StringComparison.OrdinalIgnoreCase))
        {
            using var reader = new StreamReader(
                stream,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
                detectEncodingFromByteOrderMarks: true,
                bufferSize: 81920,
                leaveOpen: true);
            var text = await reader.ReadToEndAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new DocumentExtractionException(
                    "The text document contains no extractable content.");
            }

            return TextChunker.ChunkText(
                text,
                value => embeddings.CountTokens(value, EmbeddingInputKind.Passage),
                embeddings.MaximumInputTokenCount);
        }

        throw new DocumentExtractionException(
            $"Unsupported document content type '{contentType}'.");
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null)
        {
            await _channel.CloseAsync(cancellationToken);
            await _channel.DisposeAsync();
        }

        await base.StopAsync(cancellationToken);
    }
}
