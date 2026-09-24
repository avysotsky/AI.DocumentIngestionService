namespace AI.DocumentIngestion.Application.Abstractions;

public interface IEmbeddingProvider
{
    Task<float[]> GenerateAsync(string text, CancellationToken cancellationToken);
}
