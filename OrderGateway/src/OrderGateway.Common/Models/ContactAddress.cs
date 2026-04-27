namespace OrderGateway.Common.Models;

/// <summary>
/// Represents a parsed contact address with optional display name.
/// Used for order recipient and sender identification.
/// </summary>
public sealed class ContactAddress(string address, string? displayName = null)
{
    /// <summary>The address identifier for an order party (e.g., "ORD-ADDR-12345").</summary>
    public string Address { get; } = address;

    /// <summary>Optional display name associated with the address.</summary>
    public string? DisplayName { get; } = displayName;

    public override string ToString() =>
        string.IsNullOrEmpty(DisplayName)
            ? Address
            : $"\"{DisplayName}\" <{Address}>";
}
