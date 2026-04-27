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
                    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
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
                    options.ReportApiVersions = true;
                    options.AssumeDefaultVersionWhenUnspecified = true;
                    options.DefaultApiVersion = new ApiVersion(1, 0);
                }
            )
            .AddApiExplorer(
                options =>
                {
                    options.SubstituteApiVersionInUrl = true;
                    options.SubstitutionFormat = "F";
                    options.GroupNameFormat = "'v'VVV";
                }
            );

        return services;
    }
}
