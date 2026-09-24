namespace AI.DocumentIngestion.Infrastructure.Storage;

public sealed class FileObjectStorageOptions
{
    public const string SectionName = "ObjectStorage";

    public string RootPath { get; set; } = "data/objects";
}
