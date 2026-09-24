namespace AI.DocumentIngestion.Application.Events;

public sealed record DocumentUploadedV1(
    Guid MessageId,
    DateTimeOffset OccurredAt,
    Guid DocumentId);
