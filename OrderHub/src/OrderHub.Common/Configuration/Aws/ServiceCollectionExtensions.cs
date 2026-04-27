using Amazon.S3;
using Amazon.S3.Transfer;
using OrderHub.Common.Configuration.Aws;
using OrderHub.Common.Exceptions;
using OrderHub.Common.Services;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Microsoft.Extensions.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
    private static IServiceCollection ConfigureAws(this IServiceCollection services, IConfiguration configuration)
    {
        // Read optional AWS connection configuration
        var awsConnectionOptions = configuration
            .GetSection("Aws:Connection")
            .Get<AwsConnectionOptions>();

        // Configure S3 client with optional LocalStack support
        if (awsConnectionOptions != null)
        {
            Log.Information("Aws:Connection configured with ServiceUrl: {ServiceUrl}, Region: {Region}",
                awsConnectionOptions.ServiceUrl, awsConnectionOptions.Region);

            services.AddSingleton(awsConnectionOptions);
            services.AddSingleton<IAmazonS3>(sp =>
            {
                var options = sp.GetRequiredService<AwsConnectionOptions>();
                var config = new AmazonS3Config
                {
		            ServiceURL = options.ServiceUrl,
		            AuthenticationRegion = options.Region,
		            ForcePathStyle = true  // Required for LocalStack
		        };

                return new AmazonS3Client(config);
            });
        }
        else
        {
            Log.Information("Aws:Connection not configured, using default AWS endpoints");
            services.AddAWSService<IAmazonS3>();
        }

        services.AddSingleton<ITransferUtility, TransferUtility>();
        services.AddSingleton<IS3Service, S3Service>();

        var s3Config = configuration.GetRequiredSection("S3Config").Get<S3Config>() ??
                       throw new InvalidConfigurationException("Missing S3Config configuration");

        services.AddSingleton(s3Config);

        return services;
    }
}
