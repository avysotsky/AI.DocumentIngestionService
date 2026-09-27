using AI.DocumentIngestion.Infrastructure;
using AI.DocumentIngestion.Infrastructure.Messaging;
using AI.DocumentIngestion.Infrastructure.Persistence;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddDocumentIngestionExternalConfiguration(builder.Environment.ContentRootPath);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddOnnxEmbeddings(builder.Configuration);
builder.Services.AddHostedService<DocumentUploadedConsumer>();

var host = builder.Build();
_ = host.Services.GetRequiredService<AI.DocumentIngestion.Application.Abstractions.IEmbeddingProvider>();
await host.Services.MigrateDocumentIngestionDatabaseAsync();

host.Run();
