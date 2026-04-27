using Asp.Versioning;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Extensions.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection ConfigureApi(this IServiceCollection services)
    {
        services
            .AddControllers()
            .AddJsonOptions(
                options =>
                {
                    var jso = services.BuildServiceProvider().GetRequiredService<JsonSerializerOptions>();
                    options.JsonSerializerOptions.PropertyNameCaseInsensitive = jso.PropertyNameCaseInsensitive;
                    options.JsonSerializerOptions.PropertyNamingPolicy = jso.PropertyNamingPolicy;
                    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                    foreach (var converter in jso.Converters)
                    {
                        options.JsonSerializerOptions.Converters.Add(converter);
                    }
                }
            );

        return services;
    }

    public static IServiceCollection ConfigureVersioning(this IServiceCollection services)
    {
        services
            .AddApiVersioning(
                options =>
                {
                    var builder = new MediaTypeApiVersionReaderBuilder();
                    options.ApiVersionReader = builder.Template("application/vnd.order.v{version}+json").Build();
                    options.ReportApiVersions = true;
                    options.AssumeDefaultVersionWhenUnspecified = true;
                    options.DefaultApiVersion = new ApiVersion(1, 0);
                }
            )
            .AddApiExplorer(
                options =>
                {
                    options.GroupNameFormat = "'v'VVV";
                }
            );

        return services;
    }
}
