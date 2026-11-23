using InvoiceBuilder.Domain.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InvoiceBuilder.Application.Behaviours;

/// <summary>Logs each request's name, outcome and duration. Never logs the request body (it can hold personal data).</summary>
public sealed partial class LoggingBehaviour<TRequest, TResponse>(ILogger<LoggingBehaviour<TRequest, TResponse>> logger, TimeProvider timeProvider)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : Result
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        var name = typeof(TRequest).Name;
        var started = timeProvider.GetTimestamp();
        var response = await next(cancellationToken);
        var elapsed = timeProvider.GetElapsedTime(started).TotalMilliseconds;

        if (response.IsSuccess)
        {
            LogHandled(name, elapsed);
        }
        else
        {
            LogFailed(name, response.Error.Code, elapsed);
        }

        return response;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Handled {RequestName} in {ElapsedMs} ms")]
    private partial void LogHandled(string requestName, double elapsedMs);

    [LoggerMessage(Level = LogLevel.Information, Message = "{RequestName} failed with {ErrorCode} in {ElapsedMs} ms")]
    private partial void LogFailed(string requestName, string errorCode, double elapsedMs);
}
