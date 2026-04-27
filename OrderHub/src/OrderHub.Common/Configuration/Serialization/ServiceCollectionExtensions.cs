using OrderHub.Common.Helpers;

namespace Microsoft.Extensions.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection ConfigureJsonSerializerOptions(this IServiceCollection services)
    {
        var jsonSerializerOptions = JsonSerializationOptions.GetJsonSerializerOptions();

        services.AddSingleton(jsonSerializerOptions);

        return services;
    }
}
