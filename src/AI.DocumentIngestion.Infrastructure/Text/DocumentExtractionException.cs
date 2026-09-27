namespace AI.DocumentIngestion.Infrastructure.Text;

public sealed class DocumentExtractionException : Exception
{
    public DocumentExtractionException(string message)
        : base(message)
    {
    }

    public DocumentExtractionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
