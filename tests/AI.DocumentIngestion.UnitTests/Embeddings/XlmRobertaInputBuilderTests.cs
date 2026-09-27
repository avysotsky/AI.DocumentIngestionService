using AI.DocumentIngestion.Infrastructure.Embeddings;

namespace AI.DocumentIngestion.UnitTests.Embeddings;

public sealed class XlmRobertaInputBuilderTests
{
    [Fact]
    public void AddSpecialTokensAndTruncate_PreservesBoundariesAtTokenLimit()
    {
        var tokens = Enumerable.Range(10, 20).Select(value => (uint)value).ToArray();

        var result = XlmRobertaInputBuilder.AddSpecialTokensAndTruncate(tokens, maximumLength: 8);

        Assert.Equal(8, result.Length);
        Assert.Equal(XlmRobertaInputBuilder.BeginningOfSentenceTokenId, result[0]);
        Assert.Equal([10L, 11L, 12L, 13L, 14L, 15L], result[1..^1]);
        Assert.Equal(XlmRobertaInputBuilder.EndOfSentenceTokenId, result[^1]);
    }

    [Fact]
    public void AddSpecialTokensAndTruncate_DoesNotPadShortSequence()
    {
        var result = XlmRobertaInputBuilder.AddSpecialTokensAndTruncate([42U], maximumLength: 8);

        Assert.Equal([0L, 42L, 2L], result);
    }
}
