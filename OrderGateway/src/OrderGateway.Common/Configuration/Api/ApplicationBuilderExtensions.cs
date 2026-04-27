using CorrelationId;

namespace Microsoft.AspNetCore.Builder;

public static partial class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseApi(this IApplicationBuilder applicationBuilder)
    {
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
