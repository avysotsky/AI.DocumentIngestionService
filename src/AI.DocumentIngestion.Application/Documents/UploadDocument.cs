using System.Security.Cryptography;
using AI.DocumentIngestion.Application.Abstractions;
using AI.DocumentIngestion.Domain.Documents;

namespace AI.DocumentIngestion.Application.Documents;

public sealed record UploadDocumentCommand(
    string FileName,
    string ContentType,
    long Size,
    Stream Content,
    string TenantId,
    string OwnerId,
    string MetadataJson = "{}");

public sealed class UploadDocumentHandler
{
    public const long MaximumFileSize = 25 * 1024 * 1024;

    private static readonly Dictionary<string, string> SupportedFiles =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = "application/pdf",
            [".txt"] = "text/plain"
        };

    private readonly IDocumentRepository _documents;
    private readonly IDocumentEventPublisher _events;
    private readonly IObjectStorage _storage;
    private readonly IClock _clock;

    public UploadDocumentHandler(
        IDocumentRepository documents,
        IDocumentEventPublisher events,
        IObjectStorage storage,
        IClock clock)
    {
        _documents = documents;
        _events = events;
        _storage = storage;
        _clock = clock;
    }

    public async Task<DocumentDto> HandleAsync(
        UploadDocumentCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        Validate(command);

        var safeFileName = Path.GetFileName(command.FileName);
        var extension = Path.GetExtension(safeFileName).ToLowerInvariant();
        var documentId = Guid.NewGuid();
        var now = _clock.UtcNow;
        var storageKey = $"documents/{now:yyyy/MM}/{documentId:N}{extension}";
        var hash = await CalculateHashAsync(command.Content, cancellationToken);

        var document = Document.Create(
            documentId,
            safeFileName,
            command.ContentType,
            command.Size,
            hash,
            storageKey,
            command.TenantId,
            command.OwnerId,
            command.MetadataJson,
            now);

        await _storage.PutAsync(
            storageKey,
            command.Content,
            command.Size,
            command.ContentType,
            cancellationToken);

        try
        {
            await _documents.AddAsync(document, cancellationToken);
            await _events.EnqueueUploadedAsync(document.Id, now, cancellationToken);
            await _documents.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await TryDeleteStoredObjectAsync(storageKey);
            throw;
        }

        return DocumentDto.FromDomain(document);
    }

    private static void Validate(UploadDocumentCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.FileName))
        {
            throw new ArgumentException("File name is required.", nameof(command));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(command.Size);

        if (command.Size > MaximumFileSize)
        {
            throw new ArgumentException(
                $"File exceeds the {MaximumFileSize} byte limit.",
                nameof(command));
        }

        var extension = Path.GetExtension(command.FileName);
        if (!SupportedFiles.TryGetValue(extension, out var expectedContentType) ||
            !string.Equals(command.ContentType, expectedContentType, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Only PDF and plain-text files are supported.", nameof(command));
        }

        if (!command.Content.CanRead || !command.Content.CanSeek)
        {
            throw new ArgumentException("The document stream must be readable and seekable.", nameof(command));
        }

        if (string.IsNullOrWhiteSpace(command.TenantId) || string.IsNullOrWhiteSpace(command.OwnerId))
        {
            throw new ArgumentException("Tenant and owner scope are required.", nameof(command));
        }

        if (command.MetadataJson.Length > 32_768)
        {
            throw new ArgumentException("Metadata cannot exceed 32768 characters.", nameof(command));
        }

        try
        {
            using var metadata = System.Text.Json.JsonDocument.Parse(command.MetadataJson);
            if (metadata.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
            {
                throw new ArgumentException("Metadata must be a JSON object.", nameof(command));
            }
        }
        catch (System.Text.Json.JsonException exception)
        {
            throw new ArgumentException("Metadata must be valid JSON.", nameof(command), exception);
        }
    }

    private static async Task<string> CalculateHashAsync(
        Stream content,
        CancellationToken cancellationToken)
    {
        content.Position = 0;
        var hash = await SHA256.HashDataAsync(content, cancellationToken);
        content.Position = 0;
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private async Task TryDeleteStoredObjectAsync(string storageKey)
    {
        try
        {
            await _storage.DeleteAsync(storageKey, CancellationToken.None);
        }
        catch
        {
            // Preserve the database failure. Orphan cleanup is handled operationally.
        }
    }
}
