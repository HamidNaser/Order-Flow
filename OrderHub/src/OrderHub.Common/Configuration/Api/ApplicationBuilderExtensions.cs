using OrderHub.Common.Configuration.Middleware;
using CorrelationId;

namespace Microsoft.AspNetCore.Builder;

public static partial class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseApi(this IApplicationBuilder applicationBuilder)
    {
        applicationBuilder.UseRouting();
        applicationBuilder.UseMiddleware<ResponseHeaderVersioningMiddleware>();
        applicationBuilder.UseAuthentication();
        applicationBuilder.UseAuthorization();
        applicationBuilder.UseCorrelationId();

#if RELEASE
        applicationBuilder.UseExceptionHandler(app => app.Run(OrderHub.Common.Configuration.Error.UnknownExceptionHandler.Handle));
#endif

        applicationBuilder.UseEndpoints(
            endpointRouteBuilder => { endpointRouteBuilder.MapControllers(); }
        );

        return applicationBuilder;
    }
}
