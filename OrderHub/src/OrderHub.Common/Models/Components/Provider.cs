namespace OrderHub.Common.Models.Components;

public class Merchant
{
    public required MerchantName Name { get; set; }
    public required string OrderId { get; set; }
    public string? SourceApplication { get; set; }
}
