using AI.DocumentIngestion.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace AI.DocumentIngestion.Infrastructure.Storage;

internal sealed class FileObjectStorage : IObjectStorage
{
    private readonly string _rootPath;

    public FileObjectStorage(IOptions<FileObjectStorageOptions> options)
    {
        _rootPath = Path.GetFullPath(options.Value.RootPath);
        Directory.CreateDirectory(_rootPath);
    }

    public async Task PutAsync(
        string key,
        Stream content,
        long contentLength,
        string contentType,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        var destinationPath = ResolvePath(key);
        var directory = Path.GetDirectoryName(destinationPath)
            ?? throw new InvalidOperationException("Object path has no parent directory.");
        Directory.CreateDirectory(directory);

        var temporaryPath = $"{destinationPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var destination = new FileStream(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             81920,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await content.CopyToAsync(destination, cancellationToken);
            }

            var actualLength = new FileInfo(temporaryPath).Length;
            if (actualLength != contentLength)
            {
                throw new InvalidDataException(
                    $"Expected {contentLength} bytes, but received {actualLength} bytes.");
            }

            File.Move(temporaryPath, destinationPath, overwrite: false);
        }
        catch
        {
            File.Delete(temporaryPath);
            throw;
        }
    }

    public Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Stream stream = new FileStream(
            ResolvePath(key),
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        File.Delete(ResolvePath(key));
        return Task.CompletedTask;
    }

    private string ResolvePath(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var relativePath = key.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, relativePath));
        var rootPrefix = _rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? _rootPath
            : _rootPath + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Object key escapes the configured storage root.", nameof(key));
        }

        return fullPath;
    }
}
