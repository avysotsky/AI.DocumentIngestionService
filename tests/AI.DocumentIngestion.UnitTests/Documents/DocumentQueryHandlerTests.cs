using AI.DocumentIngestion.Application.Abstractions;
using AI.DocumentIngestion.Application.Documents;
using AI.DocumentIngestion.Domain.Documents;

namespace AI.DocumentIngestion.UnitTests.Documents;

public sealed class DocumentQueryHandlerTests
{
    [Fact]
    public async Task ListDocuments_ReturnsRequestedPageAndTotal()
    {
        var first = CreateDocument(
            "first.txt",
            new DateTimeOffset(2026, 9, 24, 10, 0, 0, TimeSpan.Zero));
        var second = CreateDocument(
            "second.txt",
            new DateTimeOffset(2026, 9, 24, 11, 0, 0, TimeSpan.Zero));
        var repository = new InMemoryDocumentRepository([second, first]);
        var handler = new ListDocumentsHandler(repository);

        var result = await handler.HandleAsync(
            new ListDocumentsQuery(Page: 2, PageSize: 1),
            CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(first.Id, result.Items[0].Id);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Page);
    }

    [Fact]
    public async Task ReprocessDocument_ReadyDocument_QueuesNewOutboxEvent()
    {
        var document = CreateDocument("contract.txt", DateTimeOffset.UtcNow);
        document.StartExtraction(DateTimeOffset.UtcNow);
        document.StartChunking();
        document.StartEmbedding();
        document.MarkReady(DateTimeOffset.UtcNow);
        var repository = new InMemoryDocumentRepository([document]);
        var events = new RecordingEventPublisher();
        var now = new DateTimeOffset(2026, 9, 26, 18, 0, 0, TimeSpan.Zero);
        var handler = new ReprocessDocumentHandler(repository, events, new FixedClock(now));

        var result = await handler.HandleAsync(document.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(DocumentStatus.Uploaded, result.Status);
        Assert.Equal(document.Id, events.DocumentId);
        Assert.Equal(now, events.OccurredAt);
        Assert.True(repository.SaveChangesCalled);
    }

    private static Document CreateDocument(string fileName, DateTimeOffset createdAt)
    {
        return Document.Create(
            Guid.NewGuid(),
            fileName,
            "text/plain",
            10,
            new string('a', 64),
            $"documents/{Guid.NewGuid():N}.txt",
            createdAt);
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
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

    private sealed class InMemoryDocumentRepository(IReadOnlyList<Document> documents)
        : IDocumentRepository
    {
        private readonly List<Document> _documents = [.. documents];

        public bool SaveChangesCalled { get; private set; }

        public Task AddAsync(Document document, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _documents.Add(document);
            return Task.CompletedTask;
        }

        public Task<Document?> GetAsync(Guid id, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_documents.SingleOrDefault(document => document.Id == id));
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
            IReadOnlyList<Document> result = _documents.Skip(skip).Take(take).ToArray();
            return Task.FromResult(result);
        }

        public Task<int> CountAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_documents.Count);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SaveChangesCalled = true;
            return Task.CompletedTask;
        }
    }
}
