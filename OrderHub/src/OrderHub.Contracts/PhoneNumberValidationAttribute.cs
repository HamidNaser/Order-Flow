using Serilog;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace OrderHub.Contracts;

public class PhoneNumberValidationAttribute() : ValidationAttribute("Phone number is invalid.")
{
    private static readonly Regex LetterRegex = new("[a-zA-Z]", RegexOptions.Compiled);

    public override bool IsValid(object? value)
    {
        var phone = value?.ToString();
        if (string.IsNullOrWhiteSpace(phone))
        {
            return true; // Allow empty/null values - use [Required] separately if needed
        }

        var containsLetters = LetterRegex.IsMatch(phone);
        if (containsLetters)
        {
            Log.ForContext<PhoneNumberValidationAttribute>()
                .Warning("Phone number contains letters: {PhoneNumber}", phone);
            return false;
        }

        // Check length validation based on + sign presence
        var startsWithPlus = phone.StartsWith('+');
        var maxLength = startsWithPlus ? 16 : 15;

        if (phone.Length > maxLength)
        {
            Log.ForContext<PhoneNumberValidationAttribute>()
                .Warning("Phone number exceeds max length ({MaxLength}): {PhoneNumber}", maxLength, phone);
            return false;
        }

        return true;
    }
}
