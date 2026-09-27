using AI.DocumentIngestion.Application.Abstractions;
using AI.DocumentIngestion.Infrastructure.Embeddings;
using Microsoft.Extensions.Options;

namespace AI.DocumentIngestion.UnitTests.Embeddings;

public sealed class OnnxEmbeddingProviderIntegrationTests
{
    [Fact]
    public void Constructor_WhenModelIsMissing_FailsFast()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        var exception = Assert.Throws<FileNotFoundException>(() =>
            new OnnxEmbeddingProvider(
                Options.Create(new OnnxEmbeddingOptions { ModelPath = missingPath })));

        Assert.EndsWith("model.onnx", exception.FileName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateAsync_WithInstalledModel_ReturnsNormalized768Vector()
    {
        var modelPath = Environment.GetEnvironmentVariable("DOCUMENT_INGESTION_MODEL_PATH")
            ?? @"D:\AI.Models\multilingual-e5-base";
        if (!File.Exists(Path.Combine(modelPath, "model.onnx")))
        {
            return;
        }

        using var provider = new OnnxEmbeddingProvider(
            Options.Create(new OnnxEmbeddingOptions
            {
                ModelPath = modelPath,
                BatchSize = 2,
                MaxTokenLength = 32,
            }));

        var results = await provider.GenerateAsync(
            ["RabbitMQ передаёт сообщения между сервисами.", "PostgreSQL хранит векторы."],
            EmbeddingInputKind.Passage,
            CancellationToken.None);

        Assert.Equal(2, results.Count);
        Assert.All(results, result =>
        {
            Assert.Equal(768, result.Vector.Length);
            Assert.InRange(result.TokenCount, 3, 32);
            var norm = MathF.Sqrt(result.Vector.Sum(value => value * value));
            Assert.Equal(1f, norm, precision: 4);
        });
    }
}
