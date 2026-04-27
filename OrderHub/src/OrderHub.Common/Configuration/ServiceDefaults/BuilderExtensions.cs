using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

public static partial class Extensions
{
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder, string applicationName) where TBuilder : IHostApplicationBuilder
    {
        var useOpenTelemetry = bool.Parse(builder.Configuration["ENABLE_OTEL"] ?? "false");

        if (useOpenTelemetry)
            builder.ConfigureOpenTelemetry();

        builder.Configuration
            .AddDecryptedInMemoryCollection(builder.Configuration)
            .AddSplunkAppNameOverrideInMemoryCollection(applicationName, builder.Configuration);

        builder.Services
            .ConfigureLogging(builder.Configuration)
            .ConfigureJsonSerializerOptions()
            .ConfigureCommon(builder.Configuration);

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(builder.Environment.ApplicationName)
                    .AddAspNetCoreInstrumentation(tracing =>
                        // Exclude health check requests from tracing
                        tracing.Filter = context =>
                            !context.Request.Path.StartsWithSegments("/ready")
                            && !context.Request.Path.StartsWithSegments("/health")
                    )
                    // Uncomment the following line to enable gRPC instrumentation (requires the OpenTelemetry.Instrumentation.GrpcNetClient package)
                    //.AddGrpcClientInstrumentation()
                    .AddHttpClientInstrumentation();
            });

        builder.AddOpenTelemetryExporters();

        var serilogWriteTargets = builder.Configuration.GetSection("Serilog:WriteTo");
        var nextSerilogConfig = new Dictionary<string, string?>
        {
            { $"Serilog:WriteTo:{serilogWriteTargets.GetChildren().Count()}:Name", "OpenTelemetry" }
        };
        builder.Configuration.AddInMemoryCollection(nextSerilogConfig);

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        return builder;
    }
}
