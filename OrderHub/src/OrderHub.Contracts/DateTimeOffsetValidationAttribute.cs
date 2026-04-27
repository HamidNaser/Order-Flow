using System.ComponentModel.DataAnnotations;

namespace OrderHub.Contracts;

public class DateTimeOffsetValidationAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var unixEpoch = DateTimeOffset.UnixEpoch;

        return value switch
        {
            null => ValidationResult.Success,
            DateTimeOffset dateTimeOffset => dateTimeOffset > unixEpoch
                ? ValidationResult.Success
                : new ValidationResult($"The date and time must be greater than the Unix epoch ({unixEpoch:O})."),
            _ => new ValidationResult("Invalid data type for date and time.")
        };
    }
}
