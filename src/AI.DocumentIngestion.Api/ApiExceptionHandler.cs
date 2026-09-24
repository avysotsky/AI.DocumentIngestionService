using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AI.DocumentIngestion.Api;

public sealed class ApiExceptionHandler : IExceptionHandler
{
    private static readonly Action<ILogger, Exception?> LogValidationFailure =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(1001, nameof(LogValidationFailure)),
            "Document request validation failed.");

    private static readonly Action<ILogger, Exception?> LogUnhandledError =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(1002, nameof(LogUnhandledError)),
            "Unhandled document-ingestion error.");

    private readonly ILogger<ApiExceptionHandler> _logger;

    public ApiExceptionHandler(ILogger<ApiExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var isClientError = exception is ArgumentException or InvalidDataException;
        var statusCode = isClientError
            ? StatusCodes.Status400BadRequest
            : StatusCodes.Status500InternalServerError;

        if (isClientError)
        {
            LogValidationFailure(_logger, exception);
        }
        else
        {
            LogUnhandledError(_logger, exception);
        }

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = statusCode,
                Title = isClientError ? "Invalid document request" : "Internal server error",
                Detail = isClientError ? exception.Message : null,
                Instance = httpContext.Request.Path
            },
            cancellationToken);

        return true;
    }
}
