using OrderHub.Common.Models.Components;

namespace OrderHub.Common.Models;

public class DigitalOrder : ChannelOrder
{
    public required Components.Endpoints Endpoints { get; set; }
}
