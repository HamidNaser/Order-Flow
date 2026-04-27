namespace OrderGateway.IntegrationTests.TestData;

/// <summary>
/// Generates unique ContactId (GlobalCustomerId) values for integration tests.
/// The OrderGateway pipeline does not perform customer lookups (CustomerFoundStep is not wired),
/// so a valid non-zero integer is sufficient.  The mapper falls back to the ContactId from the
/// event metadata when StepContext.CustomerId is not set.
/// </summary>
internal static class CustomerTestHelper
{
    private static int _counter = 100_000;

    internal static Task<int> EnsureCustomerAndGetIdAsync(
        int storeId,
        int userId,
        string? orderAddress = null,
        string? phone = null,
        string? lastName = null)
    {
        var contactId = Interlocked.Increment(ref _counter);
        return Task.FromResult(contactId);
    }
}
