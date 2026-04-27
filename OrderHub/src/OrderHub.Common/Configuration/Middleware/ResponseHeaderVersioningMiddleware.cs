using OrderHub.Common.Configuration.Api;
using Microsoft.AspNetCore.Http;

namespace OrderHub.Common.Configuration.Middleware;

/// <summary>
/// Middleware to set the Order media type header.
/// <remarks>
/// <para>See integration-cookbooks: api-cookbook/cookbook-v2.md#8-api-versioning</para>
/// <para>
/// When a response contains a response body:
/// The response MUST also contain another HTTP header X-Order-Media-Type containing the major version
/// and format information. For example, X-Order-Media-Type: order.v1; format=json.
/// </para>
/// <para>NOTE: this is currently appending it to ALL responses.</para>
/// </remarks>
/// </summary>
public class ResponseHeaderVersioningMiddleware(RequestDelegate next)
{
    public async Task Invoke(HttpContext context)
    {
        context.Response.OnStarting(
            state =>
            {
                var version = context.GetRequestedApiVersion();
                var versionHeaderValue =
                    version?.MajorVersion != null
                        ? $"order.v{version.MajorVersion}; format=json"
                        : ApiConstants.DefaultOrderMediaTypeHeaderValue;

                var httpContext = (HttpContext)state;

                httpContext.Response.Headers[ApiConstants.OrderMediaTypeHeaderName] = versionHeaderValue;

                return Task.CompletedTask;
            },
            context
        );

        await next(context);
    }
}
