using Microsoft.AspNetCore.Http;

namespace OrderHub.Common.Configuration.Error;

public class UnknownExceptionHandler
{
    internal static Task Handle(HttpContext context)
    {
        // Errors are logged automatically
        // Return error status without leaking exception details to the caller
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        return Task.CompletedTask;
    }
}
