namespace OrderHub.Common.Exceptions;

public class InvalidOpenApiDocException : Exception
{
    public InvalidOpenApiDocException()
    {
    }

    public InvalidOpenApiDocException(string? message) : base(message)
    {
    }

    public InvalidOpenApiDocException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
