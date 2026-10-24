using System.Diagnostics;

namespace StockManager.Service.Middlewares;

/// <summary>Logs every request (method, path, status, duration, user) and propagates a correlation id.</summary>
public class LoggingMiddleware(RequestDelegate next, ILogger<LoggingMiddleware> logger)
{
    public const string CorrelationHeader = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationHeader] = correlationId;
            return Task.CompletedTask;
        });

        var stopwatch = Stopwatch.StartNew();

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            logger.LogInformation("HTTP {Method} {Path}{Query} started",
                context.Request.Method, context.Request.Path, context.Request.QueryString);

            try
            {
                await next(context);
            }
            finally
            {
                stopwatch.Stop();
                var status = context.Response.StatusCode;
                var level = status >= 500 ? LogLevel.Error : status >= 400 ? LogLevel.Warning : LogLevel.Information;

                logger.Log(level, "HTTP {Method} {Path} responded {StatusCode} in {Elapsed} ms (user: {User}, ip: {Ip})",
                    context.Request.Method,
                    context.Request.Path,
                    status,
                    stopwatch.ElapsedMilliseconds,
                    context.User.FindFirst("name")?.Value ?? "anonymous",
                    context.Connection.RemoteIpAddress);
            }
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        var incoming = context.Request.Headers[CorrelationHeader].FirstOrDefault();

        // accept only a short, safe value from the client (prevents log forging)
        return !string.IsNullOrWhiteSpace(incoming) && incoming.Length <= 64 && incoming.All(c => char.IsLetterOrDigit(c) || c == '-')
            ? incoming
            : context.TraceIdentifier;
    }
}
