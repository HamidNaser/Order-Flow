using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace Microsoft.AspNetCore.Builder;

public static partial class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseHealthChecks(this IApplicationBuilder app)
    {
        app.UseHealthChecks(
            "/health/ready",
            new HealthCheckOptions
            {
                Predicate = check => check.Name == "ready",
                ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
            }
        );

        app.UseHealthChecks(
            "/health/dependency",
            new HealthCheckOptions
            {
                Predicate = check => check.Name != "ready",
                ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
            }
        );

        return app;
    }
}
