using AI.DocumentIngestion.Infrastructure.Embeddings;

namespace AI.DocumentIngestion.UnitTests.Embeddings;

public sealed class EmbeddingMathTests
{
    [Fact]
    public void MeanPoolAndNormalize_ExcludesPaddingAndReturnsUnitVector()
    {
        float[] hiddenStates =
        [
            1, 0,
            3, 4,
            100, 100,
        ];
        long[] attentionMask = [1, 1, 0];

        var result = EmbeddingMath.MeanPoolAndNormalize(
            hiddenStates,
            attentionMask,
            sequenceLength: 3,
            dimensions: 2);

        Assert.Equal(0.707107f, result[0], precision: 5);
        Assert.Equal(0.707107f, result[1], precision: 5);
        Assert.Equal(1f, MathF.Sqrt(result.Sum(value => value * value)), precision: 5);
    }

    [Fact]
    public void MeanPoolAndNormalize_RejectsEmptyMask()
    {
        Assert.Throws<InvalidOperationException>(() =>
            EmbeddingMath.MeanPoolAndNormalize(
                [1f, 2f],
                [0L],
                sequenceLength: 1,
                dimensions: 2));
    }
}
