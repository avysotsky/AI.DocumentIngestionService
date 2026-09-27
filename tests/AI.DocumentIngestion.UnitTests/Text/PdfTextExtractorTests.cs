using AI.DocumentIngestion.Infrastructure.Text;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace AI.DocumentIngestion.UnitTests.Text;

public sealed class PdfTextExtractorTests
{
    [Fact]
    public async Task ExtractAsync_PreservesPageOrderAndReadableText()
    {
        await using var stream = CreatePdf("First page heading", "Second page body.");
        var extractor = CreateExtractor();

        var pages = await extractor.ExtractAsync(stream, CancellationToken.None);

        Assert.Collection(
            pages,
            page =>
            {
                Assert.Equal(1, page.PageNumber);
                Assert.Contains("First page heading", page.Text, StringComparison.Ordinal);
            },
            page =>
            {
                Assert.Equal(2, page.PageNumber);
                Assert.Contains("Second page body.", page.Text, StringComparison.Ordinal);
            });
    }

    [Fact]
    public async Task ExtractAsync_RejectsImageOnlyOrEmptyPdf()
    {
        var builder = new PdfDocumentBuilder();
        builder.AddPage(PageSize.A4);
        await using var stream = new MemoryStream(builder.Build());
        var extractor = CreateExtractor();

        var exception = await Assert.ThrowsAsync<DocumentExtractionException>(
            () => extractor.ExtractAsync(stream, CancellationToken.None));

        Assert.Contains("image-only", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExtractAsync_RejectsInvalidPdfWithClearError()
    {
        await using var stream = new MemoryStream("not a PDF"u8.ToArray());
        var extractor = CreateExtractor();

        var exception = await Assert.ThrowsAsync<DocumentExtractionException>(
            () => extractor.ExtractAsync(stream, CancellationToken.None));

        Assert.Contains("invalid", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExtractAsync_RejectsPdfOverPageLimit()
    {
        await using var stream = CreatePdf("one", "two");
        var extractor = new PdfTextExtractor(
            Options.Create(new PdfTextExtractionOptions
            {
                MaximumPages = 1,
                MaximumExtractedCharacters = 10_000,
            }));

        var exception = await Assert.ThrowsAsync<DocumentExtractionException>(
            () => extractor.ExtractAsync(stream, CancellationToken.None));

        Assert.Contains("page limit", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static PdfTextExtractor CreateExtractor()
    {
        return new PdfTextExtractor(Options.Create(new PdfTextExtractionOptions()));
    }

    private static MemoryStream CreatePdf(params string[] pageTexts)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        foreach (var pageText in pageTexts)
        {
            var page = builder.AddPage(PageSize.A4);
            page.AddText(pageText, 12, new PdfPoint(50, 750), font);
        }

        return new MemoryStream(builder.Build());
    }
}
