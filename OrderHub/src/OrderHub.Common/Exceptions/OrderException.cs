namespace OrderHub.Common.Exceptions
{
    public abstract class OrderException(string? message = null, Exception? innerException = null)
        : Exception(message, innerException);
}
