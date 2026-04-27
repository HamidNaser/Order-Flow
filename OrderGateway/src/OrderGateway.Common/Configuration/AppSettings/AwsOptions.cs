namespace OrderGateway.Common.Configuration.AppSettings;

public class AwsOptions
{
    public AwsConnectionOptions Connection { get; set; } = new();
}

public class AwsConnectionOptions
{
    public string? ServiceUrl { get; set; }
    public string Region { get; set; } = "us-east-1";
}
