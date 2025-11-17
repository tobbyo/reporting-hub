using System.Diagnostics;

namespace ReportingHub.Api.Infrastructure;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context, ICorrelationIdAccessor accessor, ILogger<CorrelationIdMiddleware> logger)
    {
        // Accept incoming or create new
        var incoming = context.Request.Headers[HeaderName].ToString();
        var correlationId = string.IsNullOrWhiteSpace(incoming)
            ? Activity.Current?.Id ?? Guid.NewGuid().ToString("N")
            : incoming;

        // Make available to app code + set response header
        accessor.CorrelationId = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        // Optional: add logging scope
        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await next(context);
        }
    }
}
