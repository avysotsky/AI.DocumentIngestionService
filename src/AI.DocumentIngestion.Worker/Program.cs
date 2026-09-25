using AI.DocumentIngestion.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddDocumentIngestionExternalConfiguration(builder.Environment.ContentRootPath);

var host = builder.Build();
host.Run();
