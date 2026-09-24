using AI.DocumentIngestion.Domain.Documents;

namespace AI.DocumentIngestion.Application.Documents;

public sealed record DocumentDto(
    Guid Id,
    string FileName,
    string ContentType,
    long Size,
    string Sha256Hash,
    DocumentStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ProcessingStartedAt,
    DateTimeOffset? ProcessedAt,
    string? ProcessingError,
    int ProcessingAttempt)
{
    public static DocumentDto FromDomain(Document document)
    {
        return new DocumentDto(
            document.Id,
            document.FileName,
            document.ContentType,
            document.Size,
            document.Sha256Hash,
            document.Status,
            document.CreatedAt,
            document.ProcessingStartedAt,
            document.ProcessedAt,
            document.ProcessingError,
            document.ProcessingAttempt);
    }
}
