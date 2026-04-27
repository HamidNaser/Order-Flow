using OrderGateway.Common.Models;

namespace OrderGateway.Common.Helpers;

/// <summary>
/// Parses address identifier strings into structured <see cref="ContactAddress"/> objects.
/// Handles plain identifiers and display-name formats (e.g., "Name &lt;identifier&gt;").
/// </summary>
public static class AddressParser
{
    /// <summary>
    /// Parses a comma-separated list of addresses into a list of <see cref="ContactAddress"/>.
    /// Supports plain format ("ORD-12345") and display-name format ("Name &lt;ORD-12345&gt;").
    /// </summary>
    public static List<ContactAddress>? ParseAddressList(string value)
    {
        try
        {
            var addresses = new List<ContactAddress>();
            var parts = value.Split(',');
            foreach (var part in parts)
            {
                var parsed = ParseAddress(part.Trim());
                if (parsed != null) addresses.Add(parsed);
            }
            return addresses.Count > 0 ? addresses : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Parses a single address identifier string into a <see cref="ContactAddress"/>.
    /// Returns null if the value cannot be parsed.
    /// </summary>
    public static ContactAddress? ParseAddress(string value)
    {
        try
        {
            value = value.Trim();
            if (string.IsNullOrWhiteSpace(value)) return null;

            // Check for "Display Name" <identifier> or Display Name <identifier> format
            var ltIndex = value.LastIndexOf('<');
            var gtIndex = value.LastIndexOf('>');
            if (ltIndex >= 0 && gtIndex > ltIndex)
            {
                var address = value[(ltIndex + 1)..gtIndex].Trim();
                var displayName = value[..ltIndex].Trim().Trim('"');
                if (string.IsNullOrWhiteSpace(address)) return null;
                return new ContactAddress(address, string.IsNullOrEmpty(displayName) ? null : displayName);
            }

            // Plain address format — any non-empty identifier
            if (value.Length >= 1)
            {
                return new ContactAddress(value);
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}
