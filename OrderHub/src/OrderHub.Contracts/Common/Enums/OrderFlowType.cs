namespace OrderHub.Contracts.Common.Enums;

/// <summary>
/// Specifies the direction of order flow between the business and the customer.
/// <remarks>
/// <para>From the user perspective:</para>
/// <list type="bullet">
///   <item><description><c>INCOMING</c> - From the customer; To the user.</description></item>
///   <item><description><c>OUTGOING</c> - From the user; To the customer.</description></item>
/// </list>
/// </remarks>
/// </summary>
/// <example>INCOMING, OUTGOING</example>
public enum OrderFlowType
{
    INCOMING,
    OUTGOING
}
