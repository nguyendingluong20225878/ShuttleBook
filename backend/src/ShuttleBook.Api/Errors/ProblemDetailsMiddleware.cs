using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ShuttleBook.Api.Errors;

public sealed class ProblemDetailsMiddleware(RequestDelegate next, ILogger<ProblemDetailsMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
            if (context.Response.StatusCode >= 400 && !context.Response.HasStarted
                && context.Response.ContentType is null && context.Response.ContentLength is null)
            {
                await WriteProblemAsync(context, context.Response.StatusCode);
            }
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client disconnected; there is no response to send.
            context.Abort();
        }
        catch (Exception exception) when (!context.Response.HasStarted && IsDatabaseConflict(exception))
        {
            logger.LogWarning("Database write conflict {ExceptionType}; trace {TraceId}.",
                exception.GetType().Name, Activity.Current?.Id ?? context.TraceIdentifier);
            context.Response.Clear();
            await WriteProblemAsync(context, StatusCodes.Status409Conflict);
        }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            logger.LogError("Unhandled request failure {ExceptionType}; trace {TraceId}.",
                exception.GetType().Name, Activity.Current?.Id ?? context.TraceIdentifier);
            context.Response.Clear();
            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError);
        }
    }

    private static bool IsDatabaseConflict(Exception exception)
    {
        const string uniqueViolation = "23505";
        const string exclusionViolation = "23P01";
        const string serializationFailure = "40001";
        const string deadlockDetected = "40P01";

        var pending = new Stack<Exception>();
        pending.Push(exception);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (current is PostgresException { SqlState: uniqueViolation or exclusionViolation or serializationFailure or deadlockDetected })
                return true;
            if (current is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions) pending.Push(inner);
            }
            else if (current.InnerException is not null)
            {
                pending.Push(current.InnerException);
            }
        }

        return false;
    }

    private static Task WriteProblemAsync(HttpContext context, int status)
    {
        context.Response.StatusCode = status;
        context.Response.Headers.CacheControl = "no-store";
        var problem = new ProblemDetails
        {
            Type = "about:blank",
            Title = ReasonPhrases.GetReasonPhrase(status),
            Status = status,
            Extensions =
            {
                ["code"] = status switch
                {
                    StatusCodes.Status404NotFound => "NOT_FOUND",
                    StatusCodes.Status405MethodNotAllowed => "METHOD_NOT_ALLOWED",
                    StatusCodes.Status401Unauthorized => "UNAUTHORIZED",
                    StatusCodes.Status403Forbidden => "FORBIDDEN",
                    StatusCodes.Status409Conflict => "STATE_CONFLICT",
                    StatusCodes.Status500InternalServerError => "INTERNAL_ERROR",
                    _ => "HTTP_ERROR"
                },
                ["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier
            }
        };
        return context.Response.WriteAsJsonAsync(problem, options: (System.Text.Json.JsonSerializerOptions?)null,
            contentType: "application/problem+json", cancellationToken: context.RequestAborted);
    }
}
