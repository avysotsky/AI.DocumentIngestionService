using Microsoft.Extensions.Configuration;

namespace AI.DocumentIngestion.Infrastructure;

public static class ExternalConfiguration
{
    public const string ConfigPathEnvironmentVariable = "DOCUMENT_INGESTION_CONFIG_PATH";
    private const string DefaultFileName = "config.json";

    public static IConfigurationBuilder AddDocumentIngestionExternalConfiguration(
        this IConfigurationBuilder configuration,
        string startDirectory)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(startDirectory);

        var configPath = ResolveConfigPath(startDirectory);
        return configuration.AddJsonFile(configPath, optional: false, reloadOnChange: true);
    }

    private static string ResolveConfigPath(string startDirectory)
    {
        var configuredPath = Environment.GetEnvironmentVariable(ConfigPathEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            var fullPath = Path.GetFullPath(configuredPath, startDirectory);
            return File.Exists(fullPath)
                ? fullPath
                : throw new FileNotFoundException(
                    $"External configuration file '{fullPath}' was not found.",
                    fullPath);
        }

        for (var directory = new DirectoryInfo(startDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, DefaultFileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException(
            $"External configuration file '{DefaultFileName}' was not found. " +
            $"Place it in the solution directory or set {ConfigPathEnvironmentVariable}.");
    }
}
