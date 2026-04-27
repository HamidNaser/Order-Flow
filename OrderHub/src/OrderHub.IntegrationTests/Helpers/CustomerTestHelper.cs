using Bogus;

namespace OrderHub.IntegrationTests.Helpers;

/// <summary>
/// Helper class for creating test customer data.
/// Generates fake customer IDs and names for integration tests since the
/// external Consumer API is not available in the local environment.
/// </summary>
public class CustomerTestHelper
{
    private static readonly Faker Faker = new();

    /// <summary>
    /// Generates a fake test customer with the specified contact information.
    /// </summary>
    /// <param name="storeId">The store ID (unused, kept for API compatibility).</param>
    /// <param name="orderAddress">Optional order address (unused, kept for API compatibility).</param>
    /// <param name="phoneNumber">Optional phone number (unused, kept for API compatibility).</param>
    /// <returns>A tuple containing a generated customer ID and name.</returns>
    public Task<(string CustomerId, string CustomerName)> CreateCustomerAsync(
        string storeId,
        string? orderAddress = null,
        string? phoneNumber = null)
    {
        var firstName = Faker.Name.FirstName();
        var lastName = Faker.Name.LastName();
        var customerName = $"{firstName} {lastName}";
        var customerId = Faker.Random.Guid().ToString();

        return Task.FromResult((customerId, customerName));
    }
}
