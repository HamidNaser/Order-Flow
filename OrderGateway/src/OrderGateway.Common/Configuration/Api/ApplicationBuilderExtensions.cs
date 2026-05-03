using CorrelationId;
using Microsoft.AspNetCore.Http;
using System.Net;
using System.Text.Json;

namespace Microsoft.AspNetCore.Builder;

public static partial class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseApi(this IApplicationBuilder applicationBuilder)
    {
        applicationBuilder.UseExceptionHandler(errorApp =>
        {
            errorApp.Run(async context =>
            {
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                context.Response.ContentType = "application/json";

                var response = new { error = "An unexpected error occurred." };
                await context.Response.WriteAsync(JsonSerializer.Serialize(response));
            });
        });

        applicationBuilder.UseRouting();
        applicationBuilder.UseAuthentication();
        applicationBuilder.UseAuthorization();
        applicationBuilder.UseCorrelationId();

        applicationBuilder.UseEndpoints(
            endpointRouteBuilder => { endpointRouteBuilder.MapControllers(); }
        );

        return applicationBuilder;
    }
}
