using AI.DocumentIngestion.Infrastructure.Text;

namespace AI.DocumentIngestion.UnitTests.Text;

public sealed class TextNormalizerTests
{
    [Fact]
    public void Normalize_ProducesStableUnicodeAndWhitespace()
    {
        var value = "  Cafe\u0301\t\tvalue\r\n\r\n\r\nnext\u0000line  ";

        var result = TextNormalizer.Normalize(value);

        Assert.Equal("Café value\n\nnextline", result);
    }

    [Fact]
    public void ChunkPages_PreservesPageNumbersAndOrder()
    {
        var pages = new[]
        {
            new ExtractedPage(1, "first page"),
            new ExtractedPage(2, "second page"),
        };

        var result = TextChunker.ChunkPages(pages, TokenCount, maximumTokens: 512);

        Assert.Collection(
            result,
            chunk =>
            {
                Assert.Equal(1, chunk.PageNumber);
                Assert.Equal("first page", chunk.Text);
            },
            chunk =>
            {
                Assert.Equal(2, chunk.PageNumber);
                Assert.Equal("second page", chunk.Text);
            });
    }

    [Fact]
    public void ChunkText_SplitsLargeInputWithinConfiguredLimit()
    {
        var text = string.Join(' ', Enumerable.Repeat("sentence.", 500));

        var result = TextChunker.ChunkText(text, TokenCount, maximumTokens: 512);

        Assert.True(result.Count > 1);
        Assert.All(result, chunk => Assert.InRange(chunk.Text.Length, 1, 510));
        Assert.All(result, chunk => Assert.InRange(TokenCount(chunk.Text), 3, 512));
        Assert.All(result, chunk => Assert.Null(chunk.PageNumber));
    }

    private static int TokenCount(string text) => text.Length + 2;
}
