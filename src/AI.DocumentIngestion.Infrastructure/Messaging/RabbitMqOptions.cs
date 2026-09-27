namespace AI.DocumentIngestion.Infrastructure.Messaging;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string HostName { get; set; } = string.Empty;

    public int Port { get; set; } = 5672;

    public string VirtualHost { get; set; } = "/";

    public string UserName { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string ExchangeName { get; set; } = "document-ingestion";
    public string QueueName { get; set; } = "document-ingestion.worker";
    public string RoutingKey { get; set; } = "document.uploaded.v1";
}
