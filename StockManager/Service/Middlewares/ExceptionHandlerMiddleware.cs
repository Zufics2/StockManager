using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StockManager.Models.Exceptions;

namespace StockManager.Service.Middlewares;

/// <summary>Global exception handling: every error is returned as RFC 7807 application/problem+json.</summary>
public class ExceptionHandlerMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlerMiddleware> logger,
    IHostEnvironment env)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client went away; nothing to report.
            logger.LogDebug("Request was cancelled by the client: {Path}", context.Request.Path);
            context.Response.StatusCode = 499;
        }
        catch (Exception ex)
        {
            if (context.Response.HasStarted)
            {
                logger.LogError(ex, "Unhandled exception after the response had started");
                throw;
            }

            await WriteProblemAsync(context, ex);
        }
    }

    private async Task WriteProblemAsync(HttpContext context, Exception ex)
    {
        ProblemDetails problem;

        switch (ex)
        {
            case ValidationException validation:
                var errors = validation.Errors
                    .GroupBy(e => string.IsNullOrEmpty(e.PropertyName) ? "request" : e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());

                problem = new ValidationProblemDetails(errors)
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "One or more validation errors occurred.",
                    Detail = "Please correct the highlighted fields and try again."
                };
                break;

            case AppException app:
                problem = new ProblemDetails { Status = app.StatusCode, Title = app.Title, Detail = app.Message };
                if (app.StatusCode >= 500)
                    logger.LogError(ex, "Application error: {Message}", app.Message);
                else
                    logger.LogInformation("Request rejected ({Status}): {Message}", app.StatusCode, app.Message);

                if (app is InsufficientStockException stock)
                {
                    problem.Extensions["requested"] = stock.Requested;
                    problem.Extensions["available"] = stock.Available;
                }
                break;

            case DbUpdateConcurrencyException:
                logger.LogWarning(ex, "Concurrency conflict");
                problem = new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Conflict",
                    Detail = "The data was modified by another request. Reload it and try again."
                };
                break;

            case DbUpdateException:
                logger.LogWarning(ex, "Database update failed");
                problem = new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Conflict",
                    Detail = "The operation conflicts with existing data (for example a duplicate or a referenced record)."
                };
                break;

            case BadHttpRequestException badRequest:
                problem = new ProblemDetails
                {
                    Status = badRequest.StatusCode,
                    Title = "Bad request",
                    Detail = badRequest.Message
                };
                break;

            default:
                logger.LogError(ex, "Unhandled exception while processing {Method} {Path}", context.Request.Method, context.Request.Path);
                problem = new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "An unexpected error occurred.",
                    Detail = env.IsDevelopment() ? ex.ToString() : "Please try again later or contact support with the trace id."
                };
                break;
        }

        problem.Instance = context.Request.Path;
        problem.Extensions["traceId"] = context.TraceIdentifier;

        context.Response.Clear();
        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(problem, problem.GetType(), options: null, contentType: "application/problem+json");
    }
}
