using System.Text.Json.Serialization;
using AI.DocumentIngestion.Api;
using AI.DocumentIngestion.Application.Documents;
using AI.DocumentIngestion.Infrastructure;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddHealthChecks();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<UploadDocumentHandler>();
builder.Services.AddScoped<GetDocumentHandler>();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

app.UseExceptionHandler();
app.MapHealthChecks("/health");
app.MapPost(
        "/documents",
        async (
            [FromForm] IFormFile file,
            UploadDocumentHandler handler,
            CancellationToken cancellationToken) =>
        {
            await using var content = file.OpenReadStream();
            var document = await handler.HandleAsync(
                new UploadDocumentCommand(
                    file.FileName,
                    file.ContentType,
                    file.Length,
                    content),
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

app.Run();

public partial class Program;
