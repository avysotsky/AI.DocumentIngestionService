namespace AI.DocumentIngestion.Domain.Documents;

public sealed class Document
{
    private Document()
    {
    }

    private Document(
        Guid id,
        string fileName,
        string contentType,
        long size,
        string sha256Hash,
        string storageKey,
        DateTimeOffset createdAt)
    {
        Id = id;
        FileName = RequireText(fileName, nameof(fileName));
        ContentType = RequireText(contentType, nameof(contentType));
        Sha256Hash = RequireText(sha256Hash, nameof(sha256Hash));
        StorageKey = RequireText(storageKey, nameof(storageKey));

        if (size <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), "Document size must be positive.");
        }

        Size = size;
        CreatedAt = createdAt;
        Status = DocumentStatus.Uploaded;
    }

    public Guid Id { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long Size { get; private set; }
    public string Sha256Hash { get; private set; } = string.Empty;
    public string StorageKey { get; private set; } = string.Empty;
    public DocumentStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ProcessingStartedAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }
    public string? ProcessingError { get; private set; }
    public int ProcessingAttempt { get; private set; }

    public static Document Create(
        Guid id,
        string fileName,
        string contentType,
        long size,
        string sha256Hash,
        string storageKey,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Document id cannot be empty.", nameof(id));
        }

        return new Document(id, fileName, contentType, size, sha256Hash, storageKey, createdAt);
    }

    public void StartExtraction(DateTimeOffset startedAt)
    {
        EnsureStatus(DocumentStatus.Uploaded);
        Status = DocumentStatus.Extracting;
        ProcessingStartedAt = startedAt;
        ProcessingError = null;
        ProcessingAttempt++;
    }

    public void StartChunking() => Transition(DocumentStatus.Extracting, DocumentStatus.Chunking);

    public void StartEmbedding() => Transition(DocumentStatus.Chunking, DocumentStatus.Embedding);

    public void MarkReady(DateTimeOffset processedAt)
    {
        EnsureStatus(DocumentStatus.Embedding);
        Status = DocumentStatus.Ready;
        ProcessedAt = processedAt;
        ProcessingError = null;
    }

    public void MarkFailed(string error)
    {
        if (Status is DocumentStatus.Ready or DocumentStatus.Failed)
        {
            throw new InvalidOperationException($"Cannot fail a document in '{Status}' status.");
        }

        Status = DocumentStatus.Failed;
        ProcessingError = RequireText(error, nameof(error));
    }

    public void QueueForReprocessing()
    {
        if (Status is not (DocumentStatus.Failed or DocumentStatus.Ready))
        {
            throw new InvalidOperationException($"Cannot reprocess a document in '{Status}' status.");
        }

        Status = DocumentStatus.Uploaded;
        ProcessingStartedAt = null;
        ProcessedAt = null;
        ProcessingError = null;
    }

    private void Transition(DocumentStatus expected, DocumentStatus next)
    {
        EnsureStatus(expected);
        Status = next;
    }

    private void EnsureStatus(DocumentStatus expected)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException(
                $"Expected document status '{expected}', but current status is '{Status}'.");
        }
    }

    private static string RequireText(string value, string parameterName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value cannot be empty.", parameterName)
            : value.Trim();
    }
}
