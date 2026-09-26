using System.Security.Cryptography;
using System.Text;
using AI.DocumentIngestion.Application.Abstractions;
using AI.DocumentIngestion.Application.Documents;
using AI.DocumentIngestion.Domain.Documents;

namespace AI.DocumentIngestion.UnitTests.Documents;

public sealed class UploadDocumentHandlerTests
{
    [Fact]
    public async Task HandleAsync_ValidTextFile_StoresMetadataObjectAndOutboxEvent()
    {
        var bytes = Encoding.UTF8.GetBytes("Payment must be completed within 30 calendar days.");
        var repository = new RecordingDocumentRepository();
        var storage = new RecordingObjectStorage();
        var events = new RecordingEventPublisher();
        var now = new DateTimeOffset(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);
        var handler = new UploadDocumentHandler(
            repository,
            events,
            storage,
            new FixedClock(now));

        await using var content = new MemoryStream(bytes);
        var result = await handler.HandleAsync(
            new UploadDocumentCommand("contract.txt", "text/plain", bytes.Length, content),
            CancellationToken.None);

        var expectedHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(DocumentStatus.Uploaded, result.Status);
        Assert.Equal(expectedHash, result.Sha256Hash);
        Assert.Equal(now, result.CreatedAt);
        Assert.Equal(bytes, storage.Content);
        Assert.Equal(result.Id, repository.Document?.Id);
        Assert.Equal(result.Id, events.DocumentId);
        Assert.Equal(now, events.OccurredAt);
        Assert.True(repository.SaveChangesCalled);
    }

    [Fact]
    public async Task HandleAsync_UnsupportedFile_DoesNotStoreAnything()
    {
        var repository = new RecordingDocumentRepository();
        var storage = new RecordingObjectStorage();
        var events = new RecordingEventPublisher();
        var handler = new UploadDocumentHandler(
            repository,
            events,
            storage,
            new FixedClock(DateTimeOffset.UtcNow));

        await using var content = new MemoryStream([1, 2, 3]);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.HandleAsync(
                new UploadDocumentCommand("image.png", "image/png", content.Length, content),
                CancellationToken.None));

        Assert.Null(repository.Document);
        Assert.Null(storage.Content);
        Assert.Null(events.DocumentId);
        Assert.False(repository.SaveChangesCalled);
    }

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTimeOffset UtcNow { get; }
    }

    private sealed class RecordingDocumentRepository : IDocumentRepository
    {
        public Document? Document { get; private set; }
        public bool SaveChangesCalled { get; private set; }

        public Task AddAsync(Document document, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Document = document;
            return Task.CompletedTask;
        }

        public Task<Document?> GetAsync(Guid id, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Document?.Id == id ? Document : null);
        }

        public Task<Document?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken)
        {
            return GetAsync(id, cancellationToken);
        }

        public Task<IReadOnlyList<Document>> ListAsync(
            int skip,
            int take,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<Document> result = Document is null ? [] : [Document];
            return Task.FromResult(result);
        }

        public Task<int> CountAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Document is null ? 0 : 1);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SaveChangesCalled = true;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingObjectStorage : IObjectStorage
    {
        public byte[]? Content { get; private set; }

        public async Task PutAsync(
            string key,
            Stream content,
            long contentLength,
            string contentType,
            CancellationToken cancellationToken)
        {
            Assert.False(string.IsNullOrWhiteSpace(key));
            Assert.False(string.IsNullOrWhiteSpace(contentType));
            await using var copy = new MemoryStream();
            await content.CopyToAsync(copy, cancellationToken);
            Content = copy.ToArray();
            Assert.Equal(contentLength, Content.LongLength);
        }

        public Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Stream stream = new MemoryStream(Content ?? []);
            return Task.FromResult(stream);
        }

        public Task DeleteAsync(string key, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Content = null;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingEventPublisher : IDocumentEventPublisher
    {
        public Guid? DocumentId { get; private set; }
        public DateTimeOffset? OccurredAt { get; private set; }

        public Task EnqueueUploadedAsync(
            Guid documentId,
            DateTimeOffset occurredAt,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DocumentId = documentId;
            OccurredAt = occurredAt;
            return Task.CompletedTask;
        }
    }
}
