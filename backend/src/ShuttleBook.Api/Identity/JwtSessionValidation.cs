using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace ShuttleBook.Api.Identity;

public static class JwtSessionValidation
{
    public static Func<TokenValidatedContext, Task> HandleRequestCancellation(
        Func<TokenValidatedContext, Task> validateSession) => async context =>
    {
        if (context.HttpContext.RequestAborted.IsCancellationRequested)
        {
            context.NoResult();
            return;
        }

        try
        {
            await validateSession(context);
            if (context.HttpContext.RequestAborted.IsCancellationRequested)
                context.NoResult();
        }
        catch (OperationCanceledException) when (context.HttpContext.RequestAborted.IsCancellationRequested)
        {
            // Navigation/polling can cancel the session query. End authentication before
            // JwtBearerHandler treats this expected client disconnect as a JWT failure.
            // Cancellation unrelated to RequestAborted still propagates and remains visible.
            context.NoResult();
        }
    };
}
