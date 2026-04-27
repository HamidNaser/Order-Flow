using OrderHub.Common.Models.Components;
using Destructurama.Attributed;

namespace OrderHub.Common.Models;

public class ShipmentOrder : ChannelOrder
{
    [LogMasked]
    public string? OrderTitle { get; set; }

    public required List<AddressInfo> To { get; set; }

    public required AddressInfo From { get; set; }
}
