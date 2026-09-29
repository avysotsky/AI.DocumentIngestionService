using System.Text.Json.Serialization;
using AI.DocumentIngestion.Api;
using AI.DocumentIngestion.Application.Documents;
using AI.DocumentIngestion.Infrastructure;
using AI.DocumentIngestion.Infrastructure.Messaging;
using AI.DocumentIngestion.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddDocumentIngestionExternalConfiguration(builder.Environment.ContentRootPath);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks()
    .AddDbContextCheck<DocumentIngestionDbContext>("postgresql")
    .AddCheck<RabbitMqHealthCheck>("rabbitmq");
builder.Services.AddScoped<UploadDocumentHandler>();
builder.Services.AddScoped<GetDocumentHandler>();
builder.Services.AddScoped<ListDocumentsHandler>();
builder.Services.AddScoped<ReprocessDocumentHandler>();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

await app.Services.MigrateDocumentIngestionDatabaseAsync();

app.UseExceptionHandler();
app.MapHealthChecks("/health/live", new()
{
    Predicate = _ => false,
});
app.MapHealthChecks("/health/ready");
app.MapPost(
        "/documents",
        async (
            [FromForm] IFormFile file,
            [FromForm] string? metadata,
            [FromHeader(Name = "X-Tenant-Id")] string tenantId,
            [FromHeader(Name = "X-Owner-Id")] string ownerId,
            UploadDocumentHandler handler,
            CancellationToken cancellationToken) =>
        {
            await using var content = file.OpenReadStream();
            var document = await handler.HandleAsync(
                new UploadDocumentCommand(
                    file.FileName,
                    file.ContentType,
                    file.Length,
                    content,
                    tenantId,
                    ownerId,
                    string.IsNullOrWhiteSpace(metadata) ? "{}" : metadata),
                cancellationToken);

            return Results.Accepted($"/documents/{document.Id}", document);
        })
    .DisableAntiforgery()
    .WithMetadata(new RequestSizeLimitAttribute(UploadDocumentHandler.MaximumFileSize + 1024 * 1024));

app.MapGet(
    "/documents/{id:guid}",
    async (
        Guid id,
        GetDocumentHandler handler,
        CancellationToken cancellationToken) =>
    {
        var document = await handler.HandleAsync(id, cancellationToken);
        return document is null ? Results.NotFound() : Results.Ok(document);
    });

app.MapGet(
    "/documents",
    async (
        [FromQuery] int page,
        [FromQuery] int pageSize,
        ListDocumentsHandler handler,
        CancellationToken cancellationToken) =>
    {
        var result = await handler.HandleAsync(
            new ListDocumentsQuery(
                page == 0 ? 1 : page,
                pageSize == 0 ? 20 : pageSize),
            cancellationToken);
        return Results.Ok(result);
    });

app.MapPost(
    "/documents/{id:guid}/reprocess",
    async (
        Guid id,
        ReprocessDocumentHandler handler,
        CancellationToken cancellationToken) =>
    {
        var document = await handler.HandleAsync(id, cancellationToken);
        return document is null
            ? Results.NotFound()
            : Results.Accepted($"/documents/{document.Id}", document);
    });

app.Run();

public partial class Program;
