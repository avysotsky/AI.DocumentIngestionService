namespace AI.DocumentIngestion.Domain.Documents;

public enum DocumentStatus
{
    Uploaded = 0,
    Extracting = 1,
    Chunking = 2,
    Embedding = 3,
    Ready = 4,
    Failed = 5
}
