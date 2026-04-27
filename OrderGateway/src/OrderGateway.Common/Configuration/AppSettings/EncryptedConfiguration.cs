namespace OrderGateway.Common.Configuration.AppSettings;

public class EncryptedConfiguration
{
    public required IList<string> Keys { get; init; }
}
