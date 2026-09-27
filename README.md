# AI Document Ingestion Service

Production-style asynchronous .NET service that converts PDF and TXT documents into chunked, vectorized representations for downstream semantic-search and RAG services.

## Implemented

- Clean dependency direction: API/Worker -> Application -> Domain
- Document and chunk domain models with guarded lifecycle transitions
- POST /documents with PDF/TXT validation, 25 MiB limit, SHA-256, object persistence, metadata persistence, and 202 Accepted
- GET /documents/{id}
- GET /documents with bounded pagination
- POST /documents/{id}/reprocess for Ready/Failed documents
- separate liveness and PostgreSQL readiness checks
- PostgreSQL persistence with EF Core
- Transactional DocumentUploadedV1 outbox record and confirmed RabbitMQ publisher
- Durable RabbitMQ worker consumer with inbox-based idempotency
- TXT and text-based PDF extraction, deterministic normalization/chunking, and batched `intfloat/multilingual-e5-base` ONNX inference
- page-aware PDF chunks with explicit failures for encrypted, empty, image-only, invalid, and over-limit PDFs
- XLM-R tokenization, `passage: ` prefix, attention-mask mean pooling, and L2-normalized 768-dimensional embeddings
- atomic replacement of document chunks and pgvector `vector(768)` persistence before Ready
- EF migrations with pgvector extension, tables, constraints, foreign keys, and indexes
- Atomic local-filesystem object-storage adapter for the current development stage
- Problem Details errors and health endpoint
- GitHub Actions build and test workflow
- Unit tests for domain transitions and upload orchestration

The next storage increment replaces the local object adapter with MinIO while preserving the application interface.

## Stack

- .NET 8 / ASP.NET Core 8
- PostgreSQL 16 + pgvector
- EF Core 8 / Npgsql
- RabbitMQ 4.x
- MinIO in a later storage increment
- ONNX Runtime with `intfloat/multilingual-e5-base`
- PdfPig 0.1.16 (Apache-2.0) for local text-based PDF extraction
- OpenTelemetry, Testcontainers, Docker Compose, and GitHub Actions

See [solution architecture](docs/architecture/solution-architecture.md).

## API

- POST /documents
- GET /documents/{id}
- GET /documents?page=1&pageSize=20
- POST /documents/{id}/reprocess
- GET /health/live
- GET /health/ready

## Local development

Create the external runtime configuration from the example:

~~~powershell
Copy-Item config.example.json config.json
~~~

`config.json` contains PostgreSQL and RabbitMQ credentials, the object-storage path, PDF extraction limits, and Worker embedding options. It is ignored by Git and is shared by API and Worker. Never commit it. To keep it elsewhere, set `DOCUMENT_INGESTION_CONFIG_PATH` to its full path.

The checked-in example targets the Lenovo PostgreSQL and RabbitMQ ports but contains placeholders only. Put real credentials only in the ignored external configuration.

### Embedding model

The Worker runs inference locally; it does not call a model server. Download the pinned official model and tokenizer files to the single external model directory:

~~~powershell
.\\scripts\\Download-MultilingualE5Base.ps1
~~~

The script downloads from the official Hugging Face `intfloat/multilingual-e5-base` repository at a pinned revision, verifies every byte count and SHA-256 from the checked-in manifest, and writes `manifest.json` beside the model. The default destination is `D:\\AI.Models\\multilingual-e5-base`. Model files are not copied into the repository or build output.

Worker configuration:

~~~json
"PdfExtraction": {
  "MaximumPages": 1000,
  "MaximumExtractedCharacters": 10000000
},
"Embeddings": {
  "ModelPath": "D:\\AI.Models\\multilingual-e5-base",
  "BatchSize": 8,
  "MaxTokenLength": 512
}
~~~

Startup fails immediately when `model.onnx` or `tokenizer.json` is missing or when the model output is not 768-dimensional.

Restore tools and apply the database migration:

~~~powershell
dotnet tool restore
dotnet tool run dotnet-ef database update --project src/AI.DocumentIngestion.Infrastructure --startup-project src/AI.DocumentIngestion.Infrastructure
~~~

Architecture of the processing path:

~~~text
API upload -> PostgreSQL outbox -> RabbitMQ -> Worker
  -> TXT/PDF extraction -> Unicode/whitespace normalization -> page-aware chunks
  -> XLM-R tokenizer (`passage: `)
  -> batched ONNX inference -> masked mean pooling -> L2 normalization
  -> atomic chunks + vector(768) write -> Ready
~~~

Run the API and Worker in separate terminals:

~~~powershell
dotnet run --project src/AI.DocumentIngestion.Api
dotnet run --project src/AI.DocumentIngestion.Worker
~~~

Upload a document:

~~~powershell
curl.exe -F "file=@contract.txt;type=text/plain" http://localhost:5000/documents
curl.exe -F "file=@contract.pdf;type=application/pdf" http://localhost:5000/documents
~~~

### PDF behavior and limits

- Pages are processed in PDF page order. PdfPig's content-order extractor reconstructs lines and spacing as far as the PDF's text objects permit.
- Extracted text is normalized to Unicode NFC, line endings and horizontal whitespace are normalized, and unsafe control characters are removed.
- Chunks never cross a PDF page boundary; `page_number` is persisted for every PDF chunk.
- Defaults are 1,000 pages, 10,000,000 extracted characters, 10,000 chunks, and 25 MiB per upload. Each chunk is capped at both 1,500 characters and the configured 512-token model input budget, including the E5 prefix/special tokens.
- Encrypted/password-protected, malformed, empty, and image-only PDFs become `Failed` with an explicit `processingError`. OCR is intentionally out of scope.
- Reprocessing uses a new outbox/inbox message and atomically replaces all chunks, so retries and reprocess do not append duplicates.

## Verification

~~~powershell
dotnet build --configuration Release
dotnet test --configuration Release --no-build
~~~
