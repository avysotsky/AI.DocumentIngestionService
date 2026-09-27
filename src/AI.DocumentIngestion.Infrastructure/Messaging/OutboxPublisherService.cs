using System.Text;
using AI.DocumentIngestion.Infrastructure.Persistence;
using AI.DocumentIngestion.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace AI.DocumentIngestion.Infrastructure.Messaging;

internal sealed class OutboxPublisherService : BackgroundService
{
    private static readonly Action<ILogger, Exception?> LogDispatchCycleFailure =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(2001, nameof(LogDispatchCycleFailure)),
            "Outbox dispatch cycle failed.");

    private static readonly Action<ILogger, Guid, int, Exception?> LogPublishFailure =
        LoggerMessage.Define<Guid, int>(
            LogLevel.Warning,
            new EventId(2002, nameof(LogPublishFailure)),
            "Publishing outbox message {MessageId} failed on attempt {Attempt}.");

    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan FailureDelay = TimeSpan.FromSeconds(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqConnection _rabbitMq;
    private readonly ILogger<OutboxPublisherService> _logger;

    public OutboxPublisherService(
        IServiceScopeFactory scopeFactory,
        RabbitMqConnection rabbitMq,
        ILogger<OutboxPublisherService> logger)
    {
        _scopeFactory = scopeFactory;
        _rabbitMq = rabbitMq;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var published = await TryPublishNextAsync(stoppingToken);
                if (!published)
                {
                    await Task.Delay(IdleDelay, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                LogDispatchCycleFailure(_logger, exception);
                await Task.Delay(FailureDelay, stoppingToken);
            }
        }
    }

    private async Task<bool> TryPublishNextAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DocumentIngestionDbContext>();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var message = await dbContext.OutboxMessages
            .FromSqlRaw(
                """
                SELECT *
                FROM outbox_messages
                WHERE processed_at IS NULL
                ORDER BY occurred_at, id
                FOR UPDATE SKIP LOCKED
                LIMIT 1
                """)
            .SingleOrDefaultAsync(cancellationToken);

        if (message is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return false;
        }

        try
        {
            var connection = await _rabbitMq.GetAsync(cancellationToken);
            var channelOptions = new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true);
            await using var channel = await connection.CreateChannelAsync(
                channelOptions,
                cancellationToken);
            await RabbitMqTopology.DeclareAsync(channel, _rabbitMq.Options, cancellationToken);

            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json",
                MessageId = message.Id.ToString(),
                Type = message.Type
            };
            var body = Encoding.UTF8.GetBytes(message.Payload);

            await channel.BasicPublishAsync(
                _rabbitMq.Options.ExchangeName,
                _rabbitMq.Options.RoutingKey,
                mandatory: true,
                properties,
                body,
                cancellationToken);

            message.MarkPublished(DateTimeOffset.UtcNow);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            message.RecordFailure(Describe(exception));
            LogPublishFailure(_logger, message.Id, message.AttemptCount, exception);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static string Describe(Exception exception)
    {
        const int maximumLength = 2000;
        var description = $"{exception.GetType().Name}: {exception.Message}";
        return description.Length <= maximumLength
            ? description
            : description[..maximumLength];
    }
}
