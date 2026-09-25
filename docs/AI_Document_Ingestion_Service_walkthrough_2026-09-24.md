# AI Document Ingestion Service — разбор текущей реализации

Дата проверки: 2026-09-24

## 1. Что сейчас работает

Сервис уже выполняет первый законченный сценарий:

~~~text
TXT/PDF
  ↓ POST /documents
валидация
  ↓
SHA-256
  ↓
файл сохраняется в object storage
  ↓
Document + DocumentUploadedV1 сохраняются в PostgreSQL
  ↓
202 Accepted
  ↓
GET /documents/{id}
~~~

На момент проверки были запущены:

- API: http://127.0.0.1:5080
- PostgreSQL: 127.0.0.1:5540
- база: document_ingestion
- тестовые данные: 1 document и 1 outbox event

---

## 2. Архитектура solution

~~~text
API ──────────────┐
                  ├── Application ──> Domain
Worker ───────────┘        ↑
                           │
Infrastructure ────────────┘
~~~

### Domain

Содержит бизнес-сущности и правила, не знает о PostgreSQL, HTTP и файловой системе.

Главная модель:

    D:\Projects\AI.DocumentIngestionService\src\AI.DocumentIngestion.Domain\Documents\Document.cs

Документ проходит состояния:

~~~text
Uploaded
→ Extracting
→ Chunking
→ Embedding
→ Ready
~~~

При ошибке:

~~~text
любое обрабатываемое состояние
→ Failed
→ QueueForReprocessing()
→ Uploaded
~~~

Переходы защищены внутри модели:

- начало extraction: Document.cs, строка 64;
- переход к chunking и embeddings: строка 73;
- завершение: строка 77;
- повторная обработка: строка 96.

Нельзя перевести документ напрямую из Uploaded в Embedding.

---

## 3. Реализация POST

HTTP endpoint находится в:

    D:\Projects\AI.DocumentIngestionService\src\AI.DocumentIngestion.Api\Program.cs

Endpoint начинается со строки 22.

Он:

1. принимает multipart/form-data;
2. получает IFormFile;
3. открывает stream;
4. создаёт UploadDocumentCommand;
5. вызывает Application handler;
6. возвращает 202 Accepted;
7. помещает адрес документа в заголовок Location.

Основная логика находится в:

    D:\Projects\AI.DocumentIngestionService\src\AI.DocumentIngestion.Application\Documents\UploadDocument.cs

Метод HandleAsync начинается со строки 41.

### Валидация

Поддерживаются:

| Расширение | Content-Type |
|---|---|
| .pdf | application/pdf |
| .txt | text/plain |

Также проверяются:

- непустое имя;
- положительный размер;
- максимум 25 MiB;
- читаемый и seekable stream;
- совпадение расширения с Content-Type.

Код: UploadDocument.cs, строка 86.

### Безопасное имя и storage key

Из имени удаляется возможный путь через Path.GetFileName().

Ключ создаётся так:

~~~text
documents/2026-09/{documentId}.txt
~~~

Код: UploadDocument.cs, строка 48.

### SHA-256

Файл полностью хешируется, после чего stream возвращается в позицию 0.

Код: UploadDocument.cs, строка 115.

Это позволяет:

- проверять целостность;
- позднее реализовать duplicate detection;
- удостовериться, что сохранён исходный файл.

---

## 4. Object Storage

Пока используется полноценный локальный адаптер вместо MinIO:

    D:\Projects\AI.DocumentIngestionService\src\AI.DocumentIngestion.Infrastructure\Storage\FileObjectStorage.cs

Алгоритм записи:

1. создаётся временный файл;
2. stream копируется асинхронно;
3. проверяется реальный размер;
4. временный файл атомарно переименовывается;
5. при ошибке временный файл удаляется.

Код записи начинается со строки 16.

Также есть защита от выхода за storage root. Например, путь ../../outside.txt не сможет записаться вне выделенного каталога.

Позже этот адаптер заменим на MinioObjectStorage, не меняя Application-код.

---

## 5. PostgreSQL и EF Core

Регистрация инфраструктуры находится в:

    D:\Projects\AI.DocumentIngestionService\src\AI.DocumentIngestion.Infrastructure\DependencyInjection.cs

Там подключаются:

- DocumentIngestionDbContext;
- Npgsql;
- repository;
- object storage;
- clock;
- outbox event publisher.

DbContext:

    D:\Projects\AI.DocumentIngestionService\src\AI.DocumentIngestion.Infrastructure\Persistence\DocumentIngestionDbContext.cs

В нём определены:

~~~csharp
DbSet<Document>
DbSet<DocumentChunk>
DbSet<OutboxMessage>
~~~

Там же заявлено PostgreSQL-расширение vector.

### Таблица documents

EF-конфигурация:

    D:\Projects\AI.DocumentIngestionService\src\AI.DocumentIngestion.Infrastructure\Persistence\Documents\DocumentConfiguration.cs

Основные ограничения:

- id — primary key;
- storage_key — unique;
- size > 0;
- SHA-256 — char(64);
- индекс по hash;
- индекс по status и created_at.

### Первая миграция

    D:\Projects\AI.DocumentIngestionService\src\AI.DocumentIngestion.Infrastructure\Persistence\Migrations\20260924122734_InitialCreate.cs

Она создаёт:

- расширение vector;
- documents;
- document_chunks;
- outbox_messages;
- foreign keys;
- indexes;
- SQL constraints.

---

## 6. Transactional Outbox

Событие пока не отправляется напрямую в RabbitMQ.

Вместо этого DocumentUploadedV1 записывается в outbox_messages тем же DbContext, что и Document.

Код:

    D:\Projects\AI.DocumentIngestionService\src\AI.DocumentIngestion.Infrastructure\Persistence\Outbox\OutboxDocumentEventPublisher.cs

Один SaveChangesAsync() сохраняет:

~~~text
documents
+
outbox_messages
~~~

Это предотвращает ситуацию, когда документ записался, а событие потерялось, или наоборот.

Следующий Outbox Publisher будет читать необработанные строки, отправлять их в RabbitMQ и заполнять processed_at.

---

## 7. Проведённые HTTP-тесты

### Health check

~~~http
GET /health
~~~

Результат:

~~~http
HTTP/1.1 200 OK

Healthy
~~~

### Успешный POST

Был отправлен contract.txt размером 217 байт.

Результат:

~~~http
HTTP/1.1 202 Accepted
Location: /documents/1281b567-672b-4367-9477-65847bfac8f0
~~~

Ответ:

~~~json
{
  "id": "1281b567-672b-4367-9477-65847bfac8f0",
  "fileName": "contract.txt",
  "contentType": "text/plain",
  "size": 217,
  "sha256Hash": "96d128e483e518a8cd775dad66ccb6b62ee6fcee9d4a06e3bb27162c4a95e023",
  "status": "Uploaded",
  "processingAttempt": 0
}
~~~

### GET существующего документа

~~~http
GET /documents/1281b567-672b-4367-9477-65847bfac8f0
~~~

Результат:

~~~http
HTTP/1.1 200 OK
~~~

Возвращены те же метаданные.

### GET отсутствующего документа

Результат:

~~~http
HTTP/1.1 404 Not Found
~~~

### Недопустимое расширение

Файл был отправлен как contract.md.

Результат:

~~~http
HTTP/1.1 400 Bad Request
~~~

~~~json
{
  "title": "Invalid document request",
  "status": 400,
  "detail": "Only PDF and plain-text files are supported.",
  "instance": "/documents"
}
~~~

---

## 8. Что появилось в базе

### documents

~~~text
id                 1281b567-672b-4367-9477-65847bfac8f0
file_name          contract.txt
content_type       text/plain
size               217
sha256_hash        96d128e4...
storage_key        documents/2026-09/1281b567...txt
status             Uploaded
processing_attempt 0
~~~

### outbox_messages

~~~text
type          DocumentUploadedV1
processed_at  NULL
attempt_count 0
~~~

Payload:

~~~json
{
  "MessageId": "dc9ea919-ed24-4942-8e6e-03fe3c37060e",
  "DocumentId": "1281b567-672b-4367-9477-65847bfac8f0",
  "OccurredAt": "2026-09-24T13:05:59.597609+00:00"
}
~~~

### Количество строк

~~~text
documents:       1
document_chunks: 0
outbox_messages: 1
~~~

document_chunks = 0 ожидаемо: processing worker ещё не реализован.

---

## 9. Проверка сохранённого файла

Хеш исходного и сохранённого файла:

~~~text
original: 96d128e483e518a8cd775dad66ccb6b62ee6fcee9d4a06e3bb27162c4a95e023
stored:   96d128e483e518a8cd775dad66ccb6b62ee6fcee9d4a06e3bb27162c4a95e023
~~~

Побайтовое сравнение также прошло.

---

## 10. Автоматические тесты

Тесты upload-handler находятся в:

    D:\Projects\AI.DocumentIngestionService\tests\AI.DocumentIngestion.UnitTests\Documents\UploadDocumentHandlerTests.cs

Проверяется:

- успешное сохранение файла;
- правильный SHA-256;
- создание Document;
- создание outbox event;
- вызов SaveChanges;
- отклонение неподдерживаемого файла;
- отсутствие частичных данных при ошибке валидации.

Результат:

~~~text
Passed: 5
Failed: 0
Skipped: 0
~~~

Дополнительно вручную проверены SQL constraints:

- size = 0 отклонён ck_documents_size_positive;
- повторяющийся storage_key отклонён unique index.

---

## 11. Команды для самостоятельной проверки

### Health

~~~powershell
curl.exe http://127.0.0.1:5080/health
~~~

### POST

~~~powershell
curl.exe -F "file=@D:\Projects\AI.DocumentIngestionService\.local\samples\contract.txt;type=text/plain" http://127.0.0.1:5080/documents
~~~

### GET

~~~powershell
curl.exe http://127.0.0.1:5080/documents/1281b567-672b-4367-9477-65847bfac8f0
~~~

### Посмотреть документы в БД

~~~powershell
E:\PostgreSQL\16\bin\psql.exe -h 127.0.0.1 -p 55432 -U document_ingestion -d document_ingestion -c "SELECT id,file_name,status,storage_key,created_at FROM documents;"
~~~

### Посмотреть Outbox

~~~powershell
E:\PostgreSQL\16\bin\psql.exe -h 127.0.0.1 -p 55432 -U document_ingestion -d document_ingestion -c "SELECT id,type,payload,processed_at FROM outbox_messages;"
~~~

---

## 12. Важные выводы перед продолжением

1. Основной upload-flow работает end-to-end.
2. Document и Outbox сохраняются одной транзакцией EF Core.
3. Файл действительно сохраняется и совпадает с оригиналом.
4. Статус остаётся Uploaded, потому что worker ещё не создан.
5. Текущий /health проверяет только процесс API, но не PostgreSQL. Нужно добавить readiness check БД.
6. Outbox JSON сейчас PascalCase, хотя внешний контракт логичнее зафиксировать в camelCase до подключения RabbitMQ.
7. На установленном Windows PostgreSQL отсутствует расширение pgvector. Поэтому для живого теста поднят отдельный локальный PostgreSQL на порту 55432, а из тестового SQL исключена только команда установки vector. Остальная схема применена полностью.
8. Миграция проекта содержит правильный CREATE EXTENSION IF NOT EXISTS vector. Полноценно проверим её через контейнер pgvector/pgvector, когда будет доступен Docker.
