namespace OrderHub.Common.Exceptions;

public class InvalidChannelMappingException(string mapperClass, string mapperFunction, string channelType)
    : OrderException(
        $"Attempted Invalid Channel Mapping: {mapperClass}.{mapperFunction} was passed invalid type: {channelType}"
    );
