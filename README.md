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
- Transactional DocumentUploadedV1 outbox record
- First EF migration with pgvector extension, tables, constraints, foreign keys, and indexes
- Atomic local-filesystem object-storage adapter for the current development stage
- Problem Details errors and health endpoint
- GitHub Actions build and test workflow
- Unit tests for domain transitions and upload orchestration

The next storage increment replaces the local object adapter with MinIO while preserving the application interface.

## Stack

- .NET 8 / ASP.NET Core 8
- PostgreSQL 16 + pgvector
- EF Core 8 / Npgsql
- RabbitMQ and MinIO in the next processing increment
- ONNX Runtime with a multilingual embedding model in the processing increment
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

`config.json` contains the local PostgreSQL connection string and object-storage path. It is ignored by Git. To keep it elsewhere, set `DOCUMENT_INGESTION_CONFIG_PATH` to its full path.

Start PostgreSQL:

~~~powershell
docker compose up -d postgres
~~~

Restore tools and apply the database migration:

~~~powershell
dotnet tool restore
dotnet tool run dotnet-ef database update --project src/AI.DocumentIngestion.Infrastructure --startup-project src/AI.DocumentIngestion.Infrastructure
~~~

Run the API:

~~~powershell
dotnet run --project src/AI.DocumentIngestion.Api
~~~

Upload a text document:

~~~powershell
curl.exe -F "file=@contract.txt;type=text/plain" http://localhost:5000/documents
~~~

## Verification

~~~powershell
dotnet build --configuration Release
dotnet test --configuration Release --no-build
~~~
