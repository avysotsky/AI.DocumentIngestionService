namespace AI.DocumentIngestion.Domain.Documents;

public sealed class DocumentChunk
{
    private DocumentChunk()
    {
    }

    private DocumentChunk(
        Guid id,
        Guid documentId,
        int sequence,
        string text,
        int? pageNumber,
        int tokenCount)
    {
        Id = id;
        DocumentId = documentId;
        Sequence = sequence;
        Text = text;
        PageNumber = pageNumber;
        TokenCount = tokenCount;
    }

    public Guid Id { get; private set; }
    public Guid DocumentId { get; private set; }
    public int Sequence { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public int? PageNumber { get; private set; }
    public int TokenCount { get; private set; }

    public static DocumentChunk Create(
        Guid id,
        Guid documentId,
        int sequence,
        string text,
        int? pageNumber,
        int tokenCount)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Chunk id cannot be empty.", nameof(id));
        }

        if (documentId == Guid.Empty)
        {
            throw new ArgumentException("Document id cannot be empty.", nameof(documentId));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(sequence);

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Chunk text cannot be empty.", nameof(text));
        }

        if (pageNumber is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageNumber));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tokenCount);

        return new DocumentChunk(id, documentId, sequence, text.Trim(), pageNumber, tokenCount);
    }
}
