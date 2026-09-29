# Solution architecture

## Boundary

The service owns document ingestion and preparation. Semantic search and question answering are separate services.

## Projects and dependency direction

~~~text
AI.DocumentIngestion.Api -----------┐
                                    ├── Application ──> Domain
AI.DocumentIngestion.Worker --------┘       ^
            │                               │
            └────────> Infrastructure ──────┘
~~~

- **Domain**: document lifecycle and invariants; no infrastructure dependencies.
- **Application**: use cases and ports for persistence, object storage, messaging, text extraction, chunking, and embeddings.
- **Infrastructure**: PostgreSQL/pgvector, local object storage, RabbitMQ, PdfPig extraction, and ONNX adapters.
- **Api**: upload, query, and reprocess HTTP endpoints.
- **Worker**: idempotent asynchronous document-processing consumer.

## Processing flow

~~~text
POST /documents
  -> validate and hash file
  -> put object in the configured object storage
  -> insert document + outbox message in PostgreSQL
  -> return 202 Accepted

Outbox publisher -> RabbitMQ DocumentUploaded.v1
  -> Worker
  -> extract TXT or text-based PDF pages
  -> deterministic normalization and page-aware, model-token-budgeted chunking
  -> batched ONNX embeddings
  -> replace document chunks transactionally
  -> mark document Ready
~~~

## Reliability decisions

1. Publish through a transactional outbox so metadata and the upload event cannot diverge.
2. RabbitMQ delivery is at least once; the consumer records each event MessageId in the inbox and processes only documents still in `Uploaded` state.
3. Inbox insertion, lifecycle transitions, old-chunk deletion, new-chunk insertion, and `Ready` are committed in one database transaction, preventing partial replacement and duplicates.
4. Expected document failures are persisted as `Failed`; unexpected/transient failures are negatively acknowledged and requeued by RabbitMQ.
5. Store SHA-256 for integrity and future duplicate detection, but do not silently deduplicate in MVP.

## Initial PostgreSQL model

### documents

- id uuid primary key
- file_name varchar(255)
- content_type varchar(100)
- size bigint
- sha256_hash char(64)
- storage_key varchar(512) unique
- status varchar(32)
- tenant_id varchar(128)
- owner_id varchar(128)
- metadata jsonb
- created_at timestamptz
- processing_started_at timestamptz null
- processed_at timestamptz null
- processing_error text null
- processing_attempt int
- xmin optimistic concurrency token

### document_chunks

- id uuid primary key
- document_id uuid foreign key
- sequence int
- text text
- page_number int null
- token_count int
- embedding vector(768)
- unique (document_id, sequence)
- HNSW index on `embedding vector_cosine_ops`
- GIN expression index on `to_tsvector('simple', text)`

A composite `(tenant_id, owner_id, status, created_at)` document index supports identical isolation predicates in vector and FTS candidate branches. The schema and startup validation both require 768 dimensions for `intfloat/multilingual-e5-base`.

### outbox_messages / inbox_messages

Store message identity, type, payload, timestamps, attempt data, and processing result for reliable publication and idempotent consumption.

## Event contract

DocumentUploaded.v1:

~~~json
{
  "messageId": "uuid",
  "occurredAt": "2026-09-24T10:00:00Z",
  "documentId": "uuid"
}
~~~

Only stable identifiers cross the queue. The worker reloads current metadata from PostgreSQL.
