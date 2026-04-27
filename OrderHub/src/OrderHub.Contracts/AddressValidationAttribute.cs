using System.ComponentModel.DataAnnotations;
using Serilog;

namespace OrderHub.Contracts;

/// <summary>
/// Validates that an address identifier is a non-empty, well-formed string
/// suitable for order party identification. Rejects embedded display-name
/// formatting (angle brackets, quotes) and whitespace.
/// </summary>
public class AddressValidationAttribute() : ValidationAttribute("Address must be in a valid format.")
{
    public override bool IsValid(object? value)
    {
        var address = value?.ToString();

        if (string.IsNullOrWhiteSpace(address))
        {
            return true; // Allow empty/null values - use [Required] separately if needed
        }

        // Reject embedded display-name formatting (angle brackets or quotes)
        if (address.Contains('<') || address.Contains('>') || address.Contains('"'))
        {
            Log
                .ForContext<AddressValidationAttribute>()
                .Warning("Address contains display name characters (not allowed in bare address field): {address}", address);

            return false;
        }

        // Reject addresses with whitespace (indicates composite format or invalid identifier)
        if (address.Any(char.IsWhiteSpace))
        {
            Log
                .ForContext<AddressValidationAttribute>()
                .Warning("Address contains whitespace (invalid format): {address}", address);

            return false;
        }

        // Must be a non-trivial identifier (at least 3 characters)
        if (address.Length < 3)
        {
            Log
                .ForContext<AddressValidationAttribute>()
                .Warning("Address too short to be a valid identifier: {address}", address);

            return false;
        }

        return true;
    }
}
