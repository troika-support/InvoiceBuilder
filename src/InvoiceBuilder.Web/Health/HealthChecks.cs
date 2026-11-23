using Hangfire;
using InvoiceBuilder.Application.Templates;
using InvoiceBuilder.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace InvoiceBuilder.Web.Health;

/// <summary>
/// Readiness checks (spec hardening item 6): the database, the background job server and the PDF engine.
/// Liveness has no checks: it only says the process is answering.
/// </summary>
public static class HealthSetup
{
    public const string ReadyTag = "ready";

    public static IServiceCollection AddAppHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", HealthStatus.Unhealthy, [ReadyTag])
            .AddCheck<BackgroundJobsHealthCheck>("background-jobs", HealthStatus.Unhealthy, [ReadyTag])
            .AddCheck<PdfEngineHealthCheck>("pdf-engine", HealthStatus.Degraded, [ReadyTag]);
        return services;
    }

    public static IEndpointRouteBuilder MapAppHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        // Only the overall word (Healthy / Degraded / Unhealthy) is returned, never details, so these can stay public.
        endpoints.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = _ => false })
            .DisableRateLimiting();
        endpoints.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = c => c.Tags.Contains(ReadyTag) })
            .DisableRateLimiting();
        return endpoints;
    }
}

public sealed class DatabaseHealthCheck(AppDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            return await db.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("The database can't be reached.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("The database can't be reached.", ex);
        }
    }
}

public sealed class BackgroundJobsHealthCheck(JobStorage storage) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var servers = storage.GetMonitoringApi().Servers();
            return Task.FromResult(servers.Count > 0
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("No background job server is running; reminders, sends and PDFs will wait."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Background job storage can't be reached.", ex));
        }
    }
}

/// <summary>
/// Renders a one-line PDF. The result is kept for 5 minutes so frequent probes don't keep Chromium busy.
/// Degraded rather than Unhealthy: the app still works, but new PDFs and emails with attachments wait.
/// </summary>
public sealed class PdfEngineHealthCheck(IPdfRenderer pdf, TimeProvider time) : IHealthCheck
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(5);
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static (DateTimeOffset At, HealthCheckResult Result)? _last;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (_last is { } cached && time.GetUtcNow() - cached.At < CacheFor)
        {
            return cached.Result;
        }

        await Gate.WaitAsync(cancellationToken);
        try
        {
            if (_last is { } again && time.GetUtcNow() - again.At < CacheFor)
            {
                return again.Result;
            }

            HealthCheckResult result;
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(20));
                var bytes = await pdf.RenderAsync("<!doctype html><html><body><p>ok</p></body></html>", timeout.Token);
                result = bytes.Length > 0 ? HealthCheckResult.Healthy() : HealthCheckResult.Degraded("The PDF engine returned an empty file.");
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                result = HealthCheckResult.Degraded("The PDF engine isn't working; PDFs will wait.", ex);
            }

            _last = (time.GetUtcNow(), result);
            return result;
        }
        finally
        {
            Gate.Release();
        }
    }
}
