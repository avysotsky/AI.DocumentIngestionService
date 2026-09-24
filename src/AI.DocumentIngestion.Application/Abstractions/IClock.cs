namespace AI.DocumentIngestion.Application.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
