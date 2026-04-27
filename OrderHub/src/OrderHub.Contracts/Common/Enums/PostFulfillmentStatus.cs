namespace OrderHub.Contracts.Common.Enums;

/// <summary>
/// Represents the delivery status for a order.
/// <remarks>
/// <para>Dedicated delivery status enum for POST delivery status workflow.</para>
/// <para>The <c>IN_PROGRESS</c> value is NOT available for this workflow.</para>
/// </remarks>
/// </summary>
/// <example>SUCCESS, FAILURE</example>
public enum PostFulfillmentStatus
{
    SUCCESS,
    FAILURE
}
