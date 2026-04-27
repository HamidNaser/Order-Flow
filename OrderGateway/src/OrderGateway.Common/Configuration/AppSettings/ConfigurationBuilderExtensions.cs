using System.Text;
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using OrderGateway.Common.Configuration;
using OrderGateway.Common.Configuration.AppSettings;

namespace Microsoft.Extensions.Configuration;

public static partial class ConfigurationBuilderExtensions
{
    private const string SplunkAppNameKey = "Serilog:WriteTo:1:Args:fields:customFields:1:value";
    private const string SplunkAppNamePrefix = "OrderGateway";

    public static IConfigurationBuilder AddDecryptedInMemoryCollection(
        this IConfigurationBuilder configurationBuilder,
        IConfiguration configuration
    )
    {
        var encryptedConfiguration = configuration
            .GetSection(nameof(EncryptedConfiguration))
            .Get<EncryptedConfiguration>();

        if (encryptedConfiguration == null)
        {
            return configurationBuilder;
        }

        var kmsKeyAlias = configuration.GetValue<string>("Aws:KmsKeyAlias");
        if (string.IsNullOrWhiteSpace(kmsKeyAlias))
        {
            throw new InvalidConfigurationException("KMS key alias (Aws:KmsKeyAlias) is not configured.");
        }

        var decryptedKvps = new Dictionary<string, string?>();

#if DEBUG
        var profileName = configuration.GetValue<string>("Aws:Profile");
        AmazonKeyManagementServiceClient kmsClient;
        if (!string.IsNullOrWhiteSpace(profileName))
        {
            //when testing using localstack , control comes here and
            //credentials file should have a profile called [order-events]
            var chain = new CredentialProfileStoreChain();
            if (chain.TryGetAWSCredentials(profileName, out var awsCredentials))
            {
                kmsClient = new AmazonKeyManagementServiceClient(awsCredentials);
            }
            else
            {
                throw new InvalidConfigurationException($"Failed to load AWS credentials for profile: {profileName}. Check the credentials file.");
            }
        }
        else
        {
            //use default profile from credentials file.
            kmsClient = new AmazonKeyManagementServiceClient();
        }
#else
        AmazonKeyManagementServiceClient kmsClient = new AmazonKeyManagementServiceClient();
#endif
            foreach (var encryptedConfigurationKey in encryptedConfiguration.Keys)
            {
                var encryptedValue = configuration.GetValue<string>(encryptedConfigurationKey);

                if (string.IsNullOrWhiteSpace(encryptedValue))
                {
                    throw new InvalidConfigurationException();
                }

                var decryptRequest = new DecryptRequest
                {
                    KeyId = kmsKeyAlias,
                    CiphertextBlob = new MemoryStream(Convert.FromBase64String(encryptedValue))
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
        ApplicationName applicationName,
        IConfiguration configuration
    )
    {
        // If Serilog:WriteTo:1 is not present (running locally) then skip adding the AppNameKey for it
        if (!configuration.GetSection("Serilog:WriteTo:1").GetChildren().Any())
            return configurationBuilder;

        var kvps = new Dictionary<string, string?>
        {
            [SplunkAppNameKey] = $"{SplunkAppNamePrefix}.{applicationName.ToString()}"
        };

        configurationBuilder
            .AddInMemoryCollection(kvps);

        return configurationBuilder;
    }
}
