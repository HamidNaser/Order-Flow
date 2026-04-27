using OrderHub.Contracts.Common.Enums;

namespace OrderHub.Contracts;

public static class ChannelTypeConstants
{
    public const string DiscriminatorName = "channelType";

    public const string StandardDiscriminatorValue = nameof(ChannelType.STANDARD);
    public const string DigitalDiscriminatorValue = nameof(ChannelType.DIGITAL);
}
