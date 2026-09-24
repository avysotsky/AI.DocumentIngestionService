using AI.DocumentIngestion.Domain.Documents;

namespace AI.DocumentIngestion.UnitTests.Documents;

public sealed class DocumentTests
{
    [Fact]
    public void ProcessingPipeline_ValidTransitions_EndInReady()
    {
        var document = CreateDocument();
        var startedAt = new DateTimeOffset(2026, 9, 24, 10, 0, 0, TimeSpan.Zero);
        var finishedAt = startedAt.AddMinutes(1);

        document.StartExtraction(startedAt);
        document.StartChunking();
        document.StartEmbedding();
        document.MarkReady(finishedAt);

        Assert.Equal(DocumentStatus.Ready, document.Status);
        Assert.Equal(1, document.ProcessingAttempt);
        Assert.Equal(startedAt, document.ProcessingStartedAt);
        Assert.Equal(finishedAt, document.ProcessedAt);
        Assert.Null(document.ProcessingError);
    }

    [Fact]
    public void StartChunking_WhenDocumentWasNotExtracting_Throws()
    {
        var document = CreateDocument();

        var exception = Assert.Throws<InvalidOperationException>(document.StartChunking);

        Assert.Contains("Uploaded", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FailedDocument_CanBeQueuedAndProcessedAgain()
    {
        var document = CreateDocument();

        document.StartExtraction(DateTimeOffset.UtcNow);
        document.MarkFailed("PDF is corrupted.");
        document.QueueForReprocessing();
        document.StartExtraction(DateTimeOffset.UtcNow);

        Assert.Equal(DocumentStatus.Extracting, document.Status);
        Assert.Equal(2, document.ProcessingAttempt);
        Assert.Null(document.ProcessingError);
    }

    private static Document CreateDocument()
    {
        return Document.Create(
            Guid.NewGuid(),
            "contract.pdf",
            "application/pdf",
            1_024,
            new string('a', 64),
            "documents/contract.pdf",
            DateTimeOffset.UtcNow);
    }
}
