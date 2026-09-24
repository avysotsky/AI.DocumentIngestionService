using AI.DocumentIngestion.Application.Abstractions;

namespace AI.DocumentIngestion.Infrastructure;

internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
