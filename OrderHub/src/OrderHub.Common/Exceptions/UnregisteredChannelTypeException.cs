namespace OrderHub.Common.Exceptions;

public class UnregisteredChannelTypeException(string channelType)
    : OrderException($"Attempted to dispatch action for Unknown Channel Type: {channelType}");
