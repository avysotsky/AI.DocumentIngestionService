namespace AI.DocumentIngestion.Application.Abstractions;

public interface IEmbeddingProvider
{
    int MaximumInputTokenCount { get; }

    int CountTokens(string text, EmbeddingInputKind inputKind);

    Task<IReadOnlyList<EmbeddingResult>> GenerateAsync(
        IReadOnlyList<string> texts,
        EmbeddingInputKind inputKind,
        CancellationToken cancellationToken);
}

public enum EmbeddingInputKind
{
    Passage,
    Query,
}

public sealed record EmbeddingResult(float[] Vector, int TokenCount);
