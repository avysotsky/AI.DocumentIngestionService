namespace AI.DocumentIngestion.Infrastructure.Text;

public sealed class PdfTextExtractionOptions
{
    public const string SectionName = "PdfExtraction";

    public int MaximumPages { get; set; } = 1_000;

    public int MaximumExtractedCharacters { get; set; } = 10_000_000;
}
