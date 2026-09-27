namespace AI.DocumentIngestion.Infrastructure.Embeddings;

public sealed class OnnxEmbeddingOptions
{
    public const string SectionName = "Embeddings";
    public const int Dimensions = 768;

    public string ModelPath { get; set; } = string.Empty;
    public int BatchSize { get; set; } = 8;
    public int MaxTokenLength { get; set; } = 512;
}
