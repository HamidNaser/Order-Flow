using System.Text;
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using OrderHub.Common.Configuration.AppSettings;
using OrderHub.Common.Exceptions;

namespace Microsoft.Extensions.Configuration;

public static partial class ConfigurationBuilderExtensions
{
    private const string SplunkAppNameKey = "Serilog:WriteTo:1:Args:fields:customFields:1:value";
    private const string SplunkAppNamePrefix = "OrderHub";

    public static IConfigurationBuilder AddDecryptedInMemoryCollection(
        this IConfigurationBuilder configurationBuilder,
        IConfiguration configuration
    )
    {
        var encryptedConfiguration = configuration
            .GetSection(nameof(EncryptedConfiguration))
            .Get<EncryptedConfiguration>();

        if (encryptedConfiguration == null || encryptedConfiguration.Keys == null || encryptedConfiguration.Keys.Count == 0)
        {
            return configurationBuilder;
        }

        var kmsKeyAlias = configuration.GetValue<string>("Aws:KmsKeyAlias");
        if (string.IsNullOrWhiteSpace(kmsKeyAlias))
        {
            throw new InvalidConfigurationException("KMS key alias (Aws:KmsKeyAlias) is not configured.");
        }

        var decryptedKvps = new Dictionary<string, string?>();

        var kmsClient = new AmazonKeyManagementServiceClient();

        foreach (var encryptedConfigurationKey in encryptedConfiguration.Keys)
        {
            var encryptedValue = configuration.GetValue<string>(encryptedConfigurationKey);

            if (string.IsNullOrWhiteSpace(encryptedValue))
            {
                throw new InvalidConfigurationException();
            }

            using var memoryStream = new MemoryStream(Convert.FromBase64String(encryptedValue));
            var decryptRequest = new DecryptRequest
            {
                KeyId = kmsKeyAlias,
                CiphertextBlob = memoryStream,
            };

            var decryptResponse = kmsClient.DecryptAsync(decryptRequest).GetAwaiter().GetResult();

            var decryptedValue = Encoding.UTF8.GetString(decryptResponse.Plaintext.ToArray());

            decryptedKvps.Add(encryptedConfigurationKey, decryptedValue);
        }

        if (decryptedKvps.Count > 0)
        {
            configurationBuilder.AddInMemoryCollection(decryptedKvps);
        }

        return configurationBuilder;
    }

    public static IConfigurationBuilder AddSplunkAppNameOverrideInMemoryCollection(
        this IConfigurationBuilder configurationBuilder,
        string applicationName,
        IConfiguration configuration
    )
    {
        // If Serilog:WriteTo:1 is not present (running locally) then skip adding the AppNameKey for it
        if (!configuration.GetSection("Serilog:WriteTo:1").GetChildren().Any())
            return configurationBuilder;

        var kvps = new Dictionary<string, string?>
        {
            [SplunkAppNameKey] = $"{SplunkAppNamePrefix}.{applicationName}"
        };

        configurationBuilder
            .AddInMemoryCollection(kvps);

        return configurationBuilder;
    }
}
