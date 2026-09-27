using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace AI.DocumentIngestion.Infrastructure.Persistence;

public static class DatabaseMigration
{
    private const string AcquireLockSql =
        "SELECT pg_advisory_lock(hashtext('ai-document-ingestion-migrations'));";
    private const string ReleaseLockSql =
        "SELECT pg_advisory_unlock(hashtext('ai-document-ingestion-migrations'));";

    public static async Task MigrateDocumentIngestionDatabaseAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DocumentIngestionDbContext>();
        var connectionString = dbContext.Database.GetConnectionString()
            ?? throw new InvalidOperationException("PostgreSQL connection string is unavailable.");

        await using var lockConnection = new NpgsqlConnection(connectionString);
        await lockConnection.OpenAsync(cancellationToken);
        await ExecuteLockCommandAsync(lockConnection, AcquireLockSql, cancellationToken);
        try
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
        }
        finally
        {
            await ExecuteLockCommandAsync(
                lockConnection,
                ReleaseLockSql,
                CancellationToken.None);
        }
    }

    private static async Task ExecuteLockCommandAsync(
        NpgsqlConnection connection,
        string commandText,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(commandText, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
