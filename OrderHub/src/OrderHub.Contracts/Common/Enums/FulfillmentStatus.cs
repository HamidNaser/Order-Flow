namespace OrderHub.Contracts.Common.Enums;

/// <summary>
/// Represents the current delivery status of a order message.
/// <remarks>
/// <para>When set to <c>SUCCESS</c>, the OrderFulfilledDate field is required.</para>
/// </remarks>
/// </summary>
/// <example>SUCCESS, IN_PROGRESS, FAILURE</example>
public enum FulfillmentStatus
{
    SUCCESS,
    IN_PROGRESS,
    FAILURE
}
