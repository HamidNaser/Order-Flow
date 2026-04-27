using OrderHub.IntegrationTests.Clients.OrderApi.V1.Contracts;
using OrderHub.IntegrationTests.Clients.IngestExpressApi.V1.Contracts;
using OrderHub.IntegrationTests.IngestExpressApi.Helpers;
using Microsoft.AspNetCore.Http;
using MongoDB.Bson;
using Newtonsoft.Json;
using System.Globalization;
using System.Text.Json;
using Bogus;
using Xunit;
using Xunit.Abstractions;
using FulfillmentStatus = OrderHub.IntegrationTests.Clients.IngestExpressApi.V1.Contracts.FulfillmentStatus;
using AddressInfo = OrderHub.IntegrationTests.Clients.IngestExpressApi.V1.Contracts.AddressInfo;
using HttpValidationProblemDetails = OrderHub.IntegrationTests.Clients.IngestExpressApi.V1.Contracts.HttpValidationProblemDetails;
using MerchantName = OrderHub.IntegrationTests.Clients.IngestExpressApi.V1.Contracts.MerchantName;

namespace OrderHub.IntegrationTests.IngestExpressApi.V1;

[Collection("ApiTests")]
public class ShipmentOrderTests(ApiTestsFixture fixture, ITestOutputHelper testOutputHelper)
{

    [Fact]
    public async Task AddShipmentOrder_WithValidRequest_ReturnsAcceptedAndId_AndPersistsRecord()
    {
        // Arrange
        var request = await IngestExpressTestDataGenerator.GenerateAddShipmentOrderRequestAsync();

        // Serialize and output to console
        var jsonRequest = JsonConvert.SerializeObject(request, Formatting.Indented);
        testOutputHelper.WriteLine("Request JSON:");
        testOutputHelper.WriteLine(jsonRequest);

        // Act
        var httpResponse = await fixture.IngestExpressApiV1Client.AddShipmentOrderAsync(request);

        // Assert
        Assert.Equal(StatusCodes.Status202Accepted, httpResponse.StatusCode);
        var orderResponse = Assert.IsType<OrderResponse>(httpResponse.Result);

        var orderId = orderResponse.Id;

        Assert.NotNull(orderId);
        Assert.True(ObjectId.TryParse(orderId, out _));



        var getFullOrderHttpResponse =
            await fixture.RetryUntilExistsAsync(() =>
                fixture.OrderApiV1Client.GetFullOrderAsync(orderId, request.StoreId)
            );

        Assert.Equal(StatusCodes.Status200OK, getFullOrderHttpResponse.StatusCode);
        var getFullOrderResponse = Assert.IsType<GetFullOrderResponse>(getFullOrderHttpResponse.Result);
        var getShipmentResponse = Assert.IsType<GetShipmentResponse>(getFullOrderResponse.Order);
        Assert.Equal(orderId, getShipmentResponse.OrderId);
        Assert.Equal(nameof(Priority.EXPRESS), getShipmentResponse.Priority.ToString());
        Assert.Equal(request.From.Address, getShipmentResponse.From.Address);
        Assert.Equal(request.From.Name, getShipmentResponse.From.Name);
        Assert.Single(getShipmentResponse.To);
        Assert.Equal(request.To.Single().Address, getShipmentResponse.To.Single().Address);
        Assert.Equal(request.To.Single().Name, getShipmentResponse.To.Single().Name);
        Assert.Equal(
            $"\"{request.To.Single().Name}\" <{request.To.Single().Address}>",
            getShipmentResponse.FormattedToRecipients
        );
        Assert.Equal(request.OrderTitle, getShipmentResponse.OrderTitle);
        Assert.Equal(request.StoreId, getShipmentResponse.StoreId);
        Assert.Equal(request.CustomerId, getShipmentResponse.CustomerId);
        Assert.Equal(request.CustomerName, getShipmentResponse.CustomerName);
        Assert.Equal(request.AgentId, getShipmentResponse.AgentId);
        Assert.Equal(request.AgentName, getShipmentResponse.AgentName);
        Assert.Equal(request.OrderFlow.ToString(), getShipmentResponse.OrderFlow.ToString());
        Assert.NotNull(request.Content);
        Assert.Equal(request.Content, getFullOrderResponse.Content);
        Assert.Equal(
            request.Content[..Math.Min(10, request.Content.Length)],
            getShipmentResponse.OrderSummary?[..Math.Min(10, getShipmentResponse.OrderSummary.Length)]
        );
        Assert.Equal(
            request.OrderPlacedDate.ToUnixTimeMilliseconds(),
            getShipmentResponse.OrderPlacedDateUtc.ToUnixTimeMilliseconds()
        );
        Assert.Equal(
            request.OrderFulfilledDate?.ToUnixTimeMilliseconds(),
            getShipmentResponse.OrderFulfilledDateUtc?.ToUnixTimeMilliseconds()
        );
        Assert.Equal(request.Merchant.Name.ToString(), getShipmentResponse.Merchant.Name.ToString());
        Assert.Equal(request.Merchant.OrderId, getShipmentResponse.Merchant.OrderId);
        Assert.Equal(request.Merchant.SourceApplication, getShipmentResponse.Merchant.SourceApplication);
        Assert.Equal(request.TenantId, getShipmentResponse.TenantId);
        Assert.Equal(request.FulfillmentStatus.ToString(), getShipmentResponse.FulfillmentStatus.ToString());
        Assert.NotNull(request.Platform);
        Assert.Equal(request.Platform.Id.ToString(), getShipmentResponse.Platform?.Id.ToString());
        Assert.Equal(request.Platform.OperationId, getShipmentResponse.Platform?.OperationId);
        Assert.Equal(request.Platform.CustomerId, getShipmentResponse.Platform?.CustomerId);
        Assert.Equal(request.Platform.CustomerName, getShipmentResponse.Platform?.CustomerName);
        Assert.Equal(request.Platform.AgentId, getShipmentResponse.Platform?.AgentId);
        Assert.Equal(request.Platform.AgentName, getShipmentResponse.Platform?.AgentName);
        Assert.Equal(request.Platform.TrackingId, getShipmentResponse.Platform?.TrackingId);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task AddShipmentOrder_EmptyOrWhitespaceToAddress_ReturnsBadRequestWithProblemDetails(
        string toAddress
    )
    {
        // Arrange
        var request = await IngestExpressTestDataGenerator.GenerateAddShipmentOrderRequestAsync();
        request.To.Single().Address = toAddress;

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<IngestExpressApiV1ClientException<HttpValidationProblemDetails>>(async () =>
                {
                    await fixture.IngestExpressApiV1Client.AddShipmentOrderAsync(request);
                }
            );

        Assert.Equal(StatusCodes.Status400BadRequest, exception.StatusCode);
        var problemDetails = Assert.IsType<HttpValidationProblemDetails>(exception.Result);
        Assert.NotNull(problemDetails.Errors);
        var error = Assert.Single(problemDetails.Errors);
        const string expectedFieldName = "To[0].Address";
        Assert.Equal(expectedFieldName, error.Key);
        Assert.Equal("The Address field is required.", error.Value.First());
    }

    [Fact]
    public async Task AddShipmentOrder_EmptyToArray_ReturnsBadRequestWithProblemDetails()
    {
        // Arrange
        var request = await IngestExpressTestDataGenerator.GenerateAddShipmentOrderRequestAsync();
        request.To = new List<AddressInfo>();

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<IngestExpressApiV1ClientException<HttpValidationProblemDetails>>(async () =>
                {
                    await fixture.IngestExpressApiV1Client.AddShipmentOrderAsync(request);
                }
            );

        Assert.Equal(StatusCodes.Status400BadRequest, exception.StatusCode);
        var problemDetails = Assert.IsType<HttpValidationProblemDetails>(exception.Result);
        Assert.NotNull(problemDetails.Errors);
        var error = Assert.Single(problemDetails.Errors);
        const string expectedFieldName = "To";
        Assert.Equal(expectedFieldName, error.Key);
        Assert.Equal(
            $"The field {expectedFieldName} must be a string or array type with a minimum length of '1'.",
            error.Value.First()
        );
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task AddShipmentOrder_EmptyOrWhitespaceToFromAddress_ReturnsBadRequestWithProblemDetails(
        string fromAddress
    )
    {
        // Arrange
        var request = await IngestExpressTestDataGenerator.GenerateAddShipmentOrderRequestAsync();
        request.From.Address = fromAddress;

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<IngestExpressApiV1ClientException<HttpValidationProblemDetails>>(async () =>
                {
                    await fixture.IngestExpressApiV1Client.AddShipmentOrderAsync(request);
                }
            );

        Assert.Equal(StatusCodes.Status400BadRequest, exception.StatusCode);
        var problemDetails = Assert.IsType<HttpValidationProblemDetails>(exception.Result);
        Assert.NotNull(problemDetails.Errors);
        var error = Assert.Single(problemDetails.Errors);
        const string expectedFieldName = "From.Address";
        Assert.Equal(expectedFieldName, error.Key);
        Assert.Equal("The Address field is required.", error.Value.First());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task AddShipmentOrder_EmptyOrWhitespaceStoreId_ReturnsBadRequestWithProblemDetails(
        string storeId
    )
    {
        // Arrange
        var request = await IngestExpressTestDataGenerator.GenerateAddShipmentOrderRequestAsync();
        request.StoreId = storeId;

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<IngestExpressApiV1ClientException<HttpValidationProblemDetails>>(async () =>
                {
                    await fixture.IngestExpressApiV1Client.AddShipmentOrderAsync(request);
                }
            );

        Assert.Equal(StatusCodes.Status400BadRequest, exception.StatusCode);
        var problemDetails = Assert.IsType<HttpValidationProblemDetails>(exception.Result);
        Assert.NotNull(problemDetails.Errors);
        var error = Assert.Single(problemDetails.Errors);
        const string expectedFieldName = nameof(request.StoreId);
        Assert.Equal(expectedFieldName, error.Key);
        Assert.Equal($"The {expectedFieldName} field is required.", error.Value.First());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task AddShipmentOrder_EmptyOrWhitespaceCustomerId_ReturnsBadRequestWithProblemDetails(
        string customerId
    )
    {
        // Arrange
        var request = await IngestExpressTestDataGenerator.GenerateAddShipmentOrderRequestAsync();
        request.CustomerId = customerId;

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<IngestExpressApiV1ClientException<HttpValidationProblemDetails>>(async () =>
                {
                    await fixture.IngestExpressApiV1Client.AddShipmentOrderAsync(request);
                }
            );

        Assert.Equal(StatusCodes.Status400BadRequest, exception.StatusCode);
        var problemDetails = Assert.IsType<HttpValidationProblemDetails>(exception.Result);
        Assert.NotNull(problemDetails.Errors);
        var error = Assert.Single(problemDetails.Errors);
        const string expectedFieldName = nameof(request.CustomerId);
        Assert.Equal(expectedFieldName, error.Key);
        Assert.Equal($"The {expectedFieldName} field is required.", error.Value.First());
    }

    [Fact]
    public async Task AddShipmentOrder_WithInvalidIntValueOrderFlow_ReturnsBadRequest()
    {
        // Arrange
        var request = await IngestExpressTestDataGenerator.GenerateAddShipmentOrderRequestAsync();
        request.OrderFlow = (OrderHub.IntegrationTests.Clients.IngestExpressApi.V1.Contracts.OrderFlowType)999;

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<IngestExpressApiV1ClientException<HttpValidationProblemDetails>>(async () =>
                {
                    await fixture.IngestExpressApiV1Client.AddShipmentOrderAsync(request);
                }
            );

        Assert.Equal(StatusCodes.Status400BadRequest, exception.StatusCode);
        var problemDetails = Assert.IsType<HttpValidationProblemDetails>(exception.Result);
        Assert.NotNull(problemDetails.Errors);
        var errors = problemDetails.Errors;
        Assert.Equal(2, errors.Count);
        Assert.Equal("request", errors.Keys.First());
        Assert.Equal("The request field is required.", errors.Values.First().First());
        Assert.Equal("$.orderFlow", errors.Keys.Last());
        Assert.StartsWith(
            "The JSON value could not be converted to OrderHub.Contracts.Common.Enums.OrderFlowType. Path: $.orderFlow",
            errors.Values.Last().Last()
        );
    }

    [Theory(Skip = "The current SDK method doesn't support populating this date field with a string input.")]
    [InlineData("")]
    [InlineData(" ")]
    public async Task AddShipmentOrder_EmptyOrWhitespaceOrderPlacedDate_ReturnsBadRequestWithProblemDetails(
        string orderPlacedDate
    )
    {
        // Arrange
        var request = await IngestExpressTestDataGenerator.GenerateAddShipmentOrderRequestAsync();
        _ = orderPlacedDate;

        // In order to cover this we'll require a custom SDK method to support a request that allows a string input for this field.
        // request.OrderPlacedDate = orderPlacedDate;

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<IngestExpressApiV1ClientException<HttpValidationProblemDetails>>(async () =>
                {
                    await fixture.IngestExpressApiV1Client.AddShipmentOrderAsync(request);
                }
            );

        Assert.Equal(StatusCodes.Status400BadRequest, exception.StatusCode);
        var problemDetails = Assert.IsType<HttpValidationProblemDetails>(exception.Result);
        Assert.NotNull(problemDetails.Errors);
        var error = Assert.Single(problemDetails.Errors);
        const string expectedFieldName = nameof(request.OrderPlacedDate);
        Assert.Equal(expectedFieldName, error.Key);
        Assert.Equal($"The {expectedFieldName} field is required.", error.Value.First());
    }

    [Fact]
    public async Task AddShipmentOrder_InvalidOrderPlacedDate_ReturnsBadRequestWithProblemDetails()
    {
        // Arrange
        var request = await IngestExpressTestDataGenerator.GenerateAddShipmentOrderRequestAsync();
        request.OrderPlacedDate = DateTimeOffset.MinValue;

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<IngestExpressApiV1ClientException<HttpValidationProblemDetails>>(async () =>
                {
                    await fixture.IngestExpressApiV1Client.AddShipmentOrderAsync(request);
                }
            );

        Assert.Equal(StatusCodes.Status400BadRequest, exception.StatusCode);
        var problemDetails = Assert.IsType<HttpValidationProblemDetails>(exception.Result);
        Assert.NotNull(problemDetails.Errors);
        var error = Assert.Single(problemDetails.Errors);
        const string fieldName = nameof(request.OrderPlacedDate);
        Assert.Equal(fieldName, error.Key);
        Assert.Equal(
            "The date and time must be greater than the Unix epoch (1970-01-01T00:00:00.0000000+00:00).",
            error.Value.First()
        );
    }

    [Fact]
    public async Task AddShipmentOrder_WithNullMerchant_ReturnsBadRequest()
    {
        // Arrange
        var request = await IngestExpressTestDataGenerator.GenerateAddShipmentOrderRequestAsync();
        request.Merchant = null!;

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<IngestExpressApiV1ClientException<HttpValidationProblemDetails>>(async () =>
                {
                    await fixture.IngestExpressApiV1Client.AddShipmentOrderAsync(request);
                }
            );

        Assert.Equal(StatusCodes.Status400BadRequest, exception.StatusCode);
        var problemDetails = Assert.IsType<HttpValidationProblemDetails>(exception.Result);
        Assert.NotNull(problemDetails.Errors);
        var errors = problemDetails.Errors;
        Assert.Equal(2, errors.Count);
        Assert.Equal("$", errors.Keys.First());
        Assert.Equal("JSON deserialization for type 'OrderHub.Contracts.Ingest.AddShipmentOrderRequest' was missing required properties including: 'merchant'.", errors.Values.First().First());
        Assert.Equal("request", errors.Keys.Last());
        Assert.Equal("The request field is required.", errors.Values.Last().Last());
    }

    [Fact]
    public async Task AddShipmentOrder_WithInvalidIntValueMerchant_ReturnsBadRequest()
    {
        // Arrange
        var request = await IngestExpressTestDataGenerator.GenerateAddShipmentOrderRequestAsync();
        request.Merchant.Name = (MerchantName)999;

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<IngestExpressApiV1ClientException<HttpValidationProblemDetails>>(async () =>
                {
                    await fixture.IngestExpressApiV1Client.AddShipmentOrderAsync(request);
                }
            );

        Assert.Equal(StatusCodes.Status400BadRequest, exception.StatusCode);
        var problemDetails = Assert.IsType<HttpValidationProblemDetails>(exception.Result);
        Assert.NotNull(problemDetails.Errors);
        var errors = problemDetails.Errors;
        Assert.Equal(2, errors.Count);
        Assert.Equal("request", errors.Keys.First());
        Assert.Equal("The request field is required.", errors.Values.First().First());
        Assert.Equal("$.merchant.name", errors.Keys.Last());
        Assert.StartsWith("The JSON value could not be converted to OrderHub.Contracts.Common.Enums.MerchantName. Path: $.merchant.name", errors.Values.Last().Last());
    }

    [Fact]
    public async Task AddShipmentOrder_WithInvalidStringValueMerchant_ReturnsBadRequest()
    {
        // Arrange
        var request = await IngestExpressTestDataGenerator.GenerateAddShipmentOrderRequestAsync();

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<IngestExpressApiV1ClientException<HttpValidationProblemDetails>>(async () =>
                {
                    await fixture.IngestExpressApiV1Client.CustomAddShipmentOrderOverrideMerchantNameAsync(
                        request,
                        "dummyorg"
                    );
                }
            );

        Assert.Equal(StatusCodes.Status400BadRequest, exception.StatusCode);
        var problemDetails = Assert.IsType<HttpValidationProblemDetails>(exception.Result);
        Assert.NotNull(problemDetails.Errors);
        var errors = problemDetails.Errors;
        Assert.Equal(2, errors.Count);
        Assert.Equal("request", errors.Keys.First());
        Assert.Equal("The request field is required.", errors.Values.First().First());
        Assert.Equal("$.merchant.name", errors.Keys.Last());
        Assert.StartsWith("The JSON value could not be converted to OrderHub.Contracts.Common.Enums.MerchantName. Path: $.merchant.name", errors.Values.Last().Last());
    }

    [Fact]
    public async Task AddShipmentOrder_WithInvalidIntValueFulfillmentStatus_ReturnsBadRequest()
    {
        // Arrange
        var request = await IngestExpressTestDataGenerator.GenerateAddShipmentOrderRequestAsync();
        request.FulfillmentStatus = (FulfillmentStatus)999;

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<IngestExpressApiV1ClientException<HttpValidationProblemDetails>>(async () =>
                {
                    await fixture.IngestExpressApiV1Client.AddShipmentOrderAsync(request);
                }
            );

        Assert.Equal(StatusCodes.Status400BadRequest, exception.StatusCode);
        var problemDetails = Assert.IsType<HttpValidationProblemDetails>(exception.Result);
        Assert.NotNull(problemDetails.Errors);
        var errors = problemDetails.Errors;
        Assert.Equal(2, errors.Count);
        Assert.Equal("request", errors.Keys.First());
        Assert.Equal("The request field is required.", errors.Values.First().First());
        Assert.Equal("$.fulfillmentStatus", errors.Keys.Last());
        Assert.StartsWith("The JSON value could not be converted to OrderHub.Contracts.Common.Enums.FulfillmentStatus. Path: $.fulfillmentStatus", errors.Values.Last().Last());
    }

    [Theory]
    [InlineData("AB")]
    [InlineData("has space")]
    [InlineData("<identifier>")]
    [InlineData("\"quoted\"")]
    public async Task AddShipmentOrder_InvalidToAddress_ReturnsBadRequest(string invalidAddress)
    {
        // Arrange
        var request = await IngestExpressTestDataGenerator.GenerateAddShipmentOrderRequestAsync();
        request.To.Single().Address = invalidAddress;

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<IngestExpressApiV1ClientException<HttpValidationProblemDetails>>(async () =>
                {
                    await fixture.IngestExpressApiV1Client.AddShipmentOrderAsync(request);
                }
            );

        Assert.Equal(StatusCodes.Status400BadRequest, exception.StatusCode);
        var problemDetails = Assert.IsType<HttpValidationProblemDetails>(exception.Result);
        Assert.NotNull(problemDetails.Errors);
        var error = Assert.Single(problemDetails.Errors);
        const string expectedFieldName = "To[0].Address";
        Assert.Equal(expectedFieldName, error.Key);
        Assert.Equal("Address must be in a valid format.", error.Value.First());
    }

    [Fact]
    public async Task AddShipmentOrder_WithAnInvalidToAddressInPositionOne_ReturnsBadRequest()
    {
        // Arrange
        var request = await IngestExpressTestDataGenerator.GenerateAddShipmentOrderRequestAsync();
        request.To = [
            new AddressInfo { Address = request.To.Single().Address, Name = request.To.Single().Name },
            new AddressInfo { Address = "AB", Name = "Address Is Invalid" }
        ];

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<IngestExpressApiV1ClientException<HttpValidationProblemDetails>>(async () =>
                {
                    await fixture.IngestExpressApiV1Client.AddShipmentOrderAsync(request);
                }
            );

        Assert.Equal(StatusCodes.Status400BadRequest, exception.StatusCode);
        var problemDetails = Assert.IsType<HttpValidationProblemDetails>(exception.Result);
        Assert.NotNull(problemDetails.Errors);
        var error = Assert.Single(problemDetails.Errors);
        const string expectedFieldName = "To[1].Address";
        Assert.Equal(expectedFieldName, error.Key);
        Assert.Equal("Address must be in a valid format.", error.Value.First());
    }

    [Theory]
    [InlineData("AB")]
    [InlineData("has space")]
    [InlineData("<identifier>")]
    [InlineData("\"quoted\"")]
    public async Task AddShipmentOrder_InvalidFromAddress_ReturnsBadRequest(string invalidAddress)
    {
        // Arrange
        var request = await IngestExpressTestDataGenerator.GenerateAddShipmentOrderRequestAsync();
        request.From.Address = invalidAddress;

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<IngestExpressApiV1ClientException<HttpValidationProblemDetails>>(async () =>
                {
                    await fixture.IngestExpressApiV1Client.AddShipmentOrderAsync(request);
                }
            );

        Assert.Equal(StatusCodes.Status400BadRequest, exception.StatusCode);
        var problemDetails = Assert.IsType<HttpValidationProblemDetails>(exception.Result);
        Assert.NotNull(problemDetails.Errors);
        var error = Assert.Single(problemDetails.Errors);
        const string expectedFieldName = "From.Address";
        Assert.Equal(expectedFieldName, error.Key);
        Assert.Equal("Address must be in a valid format.", error.Value.First());
    }

    [Fact]
    public async Task AddShipmentOrder_WithNullStoreId_ReturnsBadRequest()
    {
        // Arrange
        var request = await IngestExpressTestDataGenerator.GenerateAddShipmentOrderRequestAsync();
        request.StoreId = null!;

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<IngestExpressApiV1ClientException<HttpValidationProblemDetails>>(async () =>
                {
                    await fixture.IngestExpressApiV1Client.AddShipmentOrderAsync(request);
                }
            );

        Assert.Equal(StatusCodes.Status400BadRequest, exception.StatusCode);
        var problemDetails = Assert.IsType<HttpValidationProblemDetails>(exception.Result);
        Assert.NotNull(problemDetails.Errors);
        var errors = problemDetails.Errors;
        Assert.Equal(2, errors.Count);
        Assert.Equal("$", errors.Keys.First());
        Assert.Equal("JSON deserialization for type 'OrderHub.Contracts.Ingest.AddShipmentOrderRequest' was missing required properties including: 'storeId'.", errors.Values.First().First());
        Assert.Equal("request", errors.Keys.Last());
        Assert.Equal("The request field is required.", errors.Values.Last().Last());
    }

    [Fact]
    public async Task AddShipmentOrder_WithNullCustomerId_ReturnsBadRequest()
    {
        // Arrange
        var request = await IngestExpressTestDataGenerator.GenerateAddShipmentOrderRequestAsync();
        request.CustomerId = null!;

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<IngestExpressApiV1ClientException<HttpValidationProblemDetails>>(async () =>
                {
                    await fixture.IngestExpressApiV1Client.AddShipmentOrderAsync(request);
                }
            );

        Assert.Equal(StatusCodes.Status400BadRequest, exception.StatusCode);
        var problemDetails = Assert.IsType<HttpValidationProblemDetails>(exception.Result);
        Assert.NotNull(problemDetails.Errors);
        var errors = problemDetails.Errors;
        Assert.Equal(2, errors.Count);
        Assert.Equal("$", errors.Keys.First());
        Assert.Equal("JSON deserialization for type 'OrderHub.Contracts.Ingest.AddShipmentOrderRequest' was missing required properties including: 'customerId'.", errors.Values.First().First());
        Assert.Equal("request", errors.Keys.Last());
        Assert.Equal("The request field is required.", errors.Values.Last().Last());
    }

    [Fact]
    public async Task AddShipmentOrder_WithValidMultiRecipient_ReturnsAcceptedAndId_AndPersistsRecord_AndGetResponseHasFormattedToRecipients()
    {
        // Arrange
        var request = await IngestExpressTestDataGenerator.GenerateAddShipmentOrderRequestAsync();

        var faker = new Faker();
        request.To = [
            new AddressInfo { Address = request.To.Single().Address, Name = request.To.Single().Name },
            new AddressInfo { Address = $"ORD-{faker.Random.AlphaNumeric(8).ToUpper()}" },
            new AddressInfo { Address = $"ORD-{faker.Random.AlphaNumeric(8).ToUpper()}", Name = faker.Name.FullName() }
        ];

        var expectedFormatted = string.Join(", ", request.To.Select(a =>
            string.IsNullOrEmpty(a.Name)
                ? a.Address
                : $"\"{a.Name}\" <{a.Address}>"));

        // Serialize and output to console
        var jsonRequest = JsonConvert.SerializeObject(request, Formatting.Indented);
        testOutputHelper.WriteLine("Request JSON:");
        testOutputHelper.WriteLine(jsonRequest);

        // Act
        var httpResponse = await fixture.IngestExpressApiV1Client.AddShipmentOrderAsync(request);

        // Assert
        Assert.Equal(StatusCodes.Status202Accepted, httpResponse.StatusCode);
        var orderResponse = Assert.IsType<OrderResponse>(httpResponse.Result);

        var orderId = orderResponse.Id;

        Assert.NotNull(orderId);
        Assert.True(ObjectId.TryParse(orderId, out _));



        var getFullOrderHttpResponse =
            await fixture.RetryUntilExistsAsync(() =>
                fixture.OrderApiV1Client.GetFullOrderAsync(orderId, request.StoreId)
            );

        Assert.Equal(StatusCodes.Status200OK, getFullOrderHttpResponse.StatusCode);
        var getFullOrderResponse = Assert.IsType<GetFullOrderResponse>(getFullOrderHttpResponse.Result);
        var getShipmentResponse = Assert.IsType<GetShipmentResponse>(getFullOrderResponse.Order);
        Assert.Equal(orderId, getShipmentResponse.OrderId);
        Assert.Equal(3, getShipmentResponse.To.Count);

        var requestToList = request.To.ToList();
        var responseToList = getShipmentResponse.To.ToList();
        for (var i = 0; i < requestToList.Count; i++)
        {
            Assert.Equal(requestToList[i].Address, responseToList[i].Address);
            Assert.Equal(requestToList[i].Name, responseToList[i].Name);
        }

        Assert.Equal(expectedFormatted, getShipmentResponse.FormattedToRecipients);
    }

    [Fact]
    public async Task AddShipmentOrder_WithContentOver300Characters_TruncatesContentWhenPersisted()
    {
        // Arrange
        var ingestLength = fixture.OrderSummaryConfig.TruncateLength + 100;
        var request = await IngestExpressTestDataGenerator.GenerateAddShipmentOrderRequestAsync();
        request.Content = string.Join("", Enumerable.Repeat('a', ingestLength));

        // Act
        var response = await fixture.IngestExpressApiV1Client.AddShipmentOrderAsync(request);

        // Assert
        var orderId = response.Result.Id;
        Assert.NotNull(orderId);


        var result =
            await fixture.RetryUntilExistsAsync(() =>
                fixture.OrderApiV1Client.GetFullOrderAsync(orderId, request.StoreId)
            );

        Assert.Equal(202, response.StatusCode);
        Assert.NotNull(response.Result);
        Assert.False(string.IsNullOrWhiteSpace(orderId));

        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Result);

        Assert.Equal(ingestLength, request.Content.Length);
        Assert.Equal(fixture.OrderSummaryConfig.TruncateLength, result.Result.Order.OrderSummary?.Length);
    }

    [Fact]
    public async Task AddShipmentOrder_WithTextElementsLengthEqualToMaxLength_DoesNotTruncateOrderSummary()
    {
        // Arrange
        var multiByteTextElement = "👩🏾‍🦳"; // This is a single text element but multiple Unicode code points (7 character equivalents)
        var ingestionContent = string.Join("", Enumerable.Repeat('a', fixture.OrderSummaryConfig.TruncateLength - 1)) + multiByteTextElement;

        var request = await IngestExpressTestDataGenerator.GenerateAddShipmentOrderRequestAsync();
        request.Content = ingestionContent;

        // Act
        var response = await fixture.IngestExpressApiV1Client.AddShipmentOrderAsync(request);

        // Assert
        var orderId = response.Result.Id;
        Assert.NotNull(orderId);


        var result =
            await fixture.RetryUntilExistsAsync(() =>
                fixture.OrderApiV1Client.GetFullOrderAsync(orderId, request.StoreId)
            );

        Assert.Equal(202, response.StatusCode);
        Assert.NotNull(response.Result);
        Assert.False(string.IsNullOrWhiteSpace(orderId));

        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Result);
        Assert.NotNull(result.Result.Order.OrderSummary);

        // Length is greater due to emoji, but represents truncate length amount of text elements
        var stringInfo = new StringInfo(result.Result.Order.OrderSummary);

        Assert.True(result.Result.Order.OrderSummary?.Length > fixture.OrderSummaryConfig.TruncateLength);
        Assert.Equal(fixture.OrderSummaryConfig.TruncateLength, stringInfo.LengthInTextElements);
        Assert.Equal(ingestionContent, result.Result.Order.OrderSummary);
    }

    [Fact]
    public async Task AddShipmentOrder_WithHtmlContent_StripsHtmlWhenPersisted()
    {
        // Arrange
        var request = await IngestExpressTestDataGenerator.GenerateAddShipmentOrderRequestAsync();
        request.Content = "<p>This is a <strong>test</strong> of html content</p>";

        // Act
        var response = await fixture.IngestExpressApiV1Client.AddShipmentOrderAsync(request);

        // Assert
        var orderId = response.Result.Id;
        Assert.NotNull(orderId);

        var result =
            await fixture.RetryUntilExistsAsync(() =>
                fixture.OrderApiV1Client.GetFullOrderAsync(orderId, request.StoreId)
            );

        Assert.Equal(202, response.StatusCode);
        Assert.NotNull(response.Result);
        Assert.False(string.IsNullOrWhiteSpace(orderId));

        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Result);

        Assert.Equal("This is a test of html content", result.Result.Order.OrderSummary);
    }

    [Fact]
    public async Task AddShipmentOrder_ContentWithSpecialCharacters_ReturnsAcceptedAndId()
    {
        // Arrange
        var request = await IngestExpressTestDataGenerator.GenerateAddShipmentOrderRequestAsync();
        request.Content = "Hello! 🌟 Special chars: @#$%^&*()_+ Unicode: café résumé naïve 中文 العربية 🚀💻🎉";
        request.OrderTitle = "Test OrderTitle with Special Chars: @#$%^&*()_+ 🌟";

        // Act
        var response = await fixture.IngestExpressApiV1Client.AddShipmentOrderAsync(request);

        // Assert - Verify ingestion
        Assert.Equal(StatusCodes.Status202Accepted, response.StatusCode);
        Assert.NotNull(response.Result);
        Assert.False(string.IsNullOrWhiteSpace(response.Result.Id));     

        var orderId = response.Result.Id;

        // Verify retrieval - OrderSummary contains emojis
        var result = await fixture.RetryUntilExistsAsync(() =>
            fixture.OrderApiV1Client.GetFullOrderAsync(orderId, request.StoreId)
        );

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.Result);

        var shipmentResponse = Assert.IsType<GetShipmentResponse>(result.Result.Order);
        Assert.NotNull(shipmentResponse.OrderSummary);
        Assert.Contains("🌟", shipmentResponse.OrderSummary);
        Assert.Contains("🚀", shipmentResponse.OrderSummary);
        Assert.Contains("💻", shipmentResponse.OrderSummary);
        Assert.Contains("🎉", shipmentResponse.OrderSummary);
        
        // Verify order title emoji preserved
        Assert.Equal("Test OrderTitle with Special Chars: @#$%^&*()_+ 🌟", shipmentResponse.OrderTitle);

        // Verify full content retrieval - emojis preserved exactly
        Assert.NotNull(shipmentResponse.OrderMetadata);
        Assert.NotNull(shipmentResponse.OrderMetadata.FullContentKey);
        
        var fullContentResponse = await fixture.OrderApiV1Client.GetOrderContentAsync(
            shipmentResponse.OrderMetadata.FullContentKey
        );

        Assert.Equal(StatusCodes.Status200OK, fullContentResponse.StatusCode);
        Assert.NotNull(fullContentResponse.Result);
        Assert.Equal(request.Content, fullContentResponse.Result.Content);
    }

    [Fact]
    public async Task AddShipmentOrder_WithComplexEmojiSequences_PreservesUnicodeCharacterValues()
    {
        // Arrange - Test various complex multi-byte emoji sequences
        var complexEmojis = new[]
        {
            "👨‍👩‍👧‍👦", "👩🏾‍🦳", "🏳️‍🌈", "👁️‍🗨️", "🧑‍💻", "❤️"
        };
        
        var content = $"Complex emoji test: {string.Join(" | ", complexEmojis)} Complete.";
        var request = await IngestExpressTestDataGenerator.GenerateAddShipmentOrderRequestAsync();
        request.Content = content;

        // Act
        var response = await fixture.IngestExpressApiV1Client.AddShipmentOrderAsync(request);

        // Assert - Verify ingestion
        Assert.Equal(StatusCodes.Status202Accepted, response.StatusCode);
        Assert.NotNull(response.Result);
        Assert.False(string.IsNullOrWhiteSpace(response.Result.Id));        
        
        var orderId = response.Result.Id;

        // Retrieve order
        var result = await fixture.RetryUntilExistsAsync(() =>
            fixture.OrderApiV1Client.GetFullOrderAsync(orderId, request.StoreId)
        );

        var shipmentResponse = Assert.IsType<GetShipmentResponse>(result.Result.Order);
        
        // Verify full content retrieval with proper null checks
        Assert.NotNull(shipmentResponse.OrderMetadata);
        Assert.NotNull(shipmentResponse.OrderMetadata.FullContentKey);
        
        var fullContentResponse = await fixture.OrderApiV1Client.GetOrderContentAsync(
            shipmentResponse.OrderMetadata.FullContentKey
        );

        Assert.Equal(StatusCodes.Status200OK, fullContentResponse.StatusCode);
        Assert.NotNull(fullContentResponse.Result);

        // Verify each complex emoji sequence preserved byte-for-byte
        foreach (var emoji in complexEmojis)
        {
            Assert.Contains(emoji, fullContentResponse.Result.Content);
        }
        
        // Verify exact content match (no encoding corruption)
        Assert.Equal(content, fullContentResponse.Result.Content);
    }

    [Fact]
    public async Task AddShipmentOrder_ContentWithHtml_ReturnsAcceptedAndId()
    {
        // Arrange
        var request = await IngestExpressTestDataGenerator.GenerateAddShipmentOrderRequestAsync();
        request.Content = "<html><body><h1>Test Order</h1><p>This is a test order with <strong>HTML</strong> content.</p>" +
                         "<ul><li>Item 1</li><li>Item 2</li></ul></body></html>";

        // Act
        var response = await fixture.IngestExpressApiV1Client.AddShipmentOrderAsync(request);

        // Assert
        var orderId = response.Result.Id;
        Assert.NotNull(orderId);

        Assert.Equal(StatusCodes.Status202Accepted, response.StatusCode);
        Assert.NotNull(response.Result);
        Assert.False(string.IsNullOrWhiteSpace(orderId));
    }

    [Fact]
    public async Task AddShipmentOrder_FulfillmentStatusSuccessWithNoDeliveryDate_ReturnsBadRequest()
    {
        // Arrange
        var request = await IngestExpressTestDataGenerator.GenerateAddShipmentOrderRequestAsync();
        request.FulfillmentStatus = FulfillmentStatus.SUCCESS;
        request.OrderFulfilledDate = null;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<IngestExpressApiV1ClientException<HttpValidationProblemDetails>>(async () =>
        {
            await fixture.IngestExpressApiV1Client.AddShipmentOrderAsync(request);
        });

        Assert.Equal(StatusCodes.Status400BadRequest, exception.StatusCode);
        Assert.Equal($"OrderFulfilledDate is required when FulfillmentStatus is 'SUCCESS'.", exception.Result.Errors?.First().Value.First());
    }

    [Theory]
    [InlineData(FulfillmentStatus.IN_PROGRESS)]
    [InlineData(FulfillmentStatus.FAILURE)]
    public async Task AddShipmentOrder_FulfillmentStatusNotSuccessWithDeliveryDate_ReturnsBadRequest(FulfillmentStatus fulfillmentStatus)
    {
        // Arrange
        var request = await IngestExpressTestDataGenerator.GenerateAddShipmentOrderRequestAsync();
        request.FulfillmentStatus = fulfillmentStatus;
        request.OrderFulfilledDate = DateTimeOffset.UtcNow;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<IngestExpressApiV1ClientException<HttpValidationProblemDetails>>(async () =>
        {
            await fixture.IngestExpressApiV1Client.AddShipmentOrderAsync(request);
        });

        Assert.Equal(StatusCodes.Status400BadRequest, exception.StatusCode);
        Assert.Equal("OrderFulfilledDate should be null or omitted when FulfillmentStatus is not 'SUCCESS'.", exception.Result.Errors?.First().Value.First());
    }

    [Fact]
    public async Task AddShipmentOrder_EmptySourceApplication_ReturnsAcceptedAndId_AndPersistsRecord()
    {
        // Arrange
        var request = await IngestExpressTestDataGenerator.GenerateAddShipmentOrderRequestAsync();
        request.Merchant.SourceApplication = "";

        // Act
        var response = await fixture.IngestExpressApiV1Client.AddShipmentOrderAsync(request);

        // Assert
        var orderId = response.Result.Id;
        Assert.NotNull(orderId);

        var result =
            await fixture.RetryUntilExistsAsync(() =>
                fixture.OrderApiV1Client.GetFullOrderAsync(orderId, request.StoreId)
            );

        Assert.Equal(StatusCodes.Status202Accepted, response.StatusCode);
        Assert.NotNull(response.Result);
        Assert.False(string.IsNullOrWhiteSpace(orderId));

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.Result);
    }

    [Fact]
    public async Task AddShipmentOrder_WithLongOrderTitle_ReturnsAcceptedAndId()
    {
        // Arrange
        var request = await IngestExpressTestDataGenerator.GenerateAddShipmentOrderRequestAsync();
        request.OrderTitle = new string('A', 500); // Very long order title

        // Act
        var response = await fixture.IngestExpressApiV1Client.AddShipmentOrderAsync(request);

        // Assert
        var orderId = response.Result.Id;
        Assert.NotNull(orderId);

        Assert.Equal(StatusCodes.Status202Accepted, response.StatusCode);
        Assert.NotNull(response.Result);
        Assert.False(string.IsNullOrWhiteSpace(orderId));
    }

    [Fact]
    public async Task AddShipmentOrder_OrderTitleIsNull_ReturnsAccepted()
    {
        // Arrange
        var request = await IngestExpressTestDataGenerator.GenerateAddShipmentOrderRequestAsync();
        request.OrderTitle = null;

        // Act
        var response = await fixture.IngestExpressApiV1Client.AddShipmentOrderAsync(request);

        // Assert
        Assert.Equal(StatusCodes.Status202Accepted, response.StatusCode);
        var orderId = response.Result.Id;
        Assert.NotNull(orderId);

        var result =
       await fixture.RetryUntilExistsAsync(() =>
           fixture.OrderApiV1Client.GetFullOrderAsync(orderId, request.StoreId)
       );

        Assert.Equal(StatusCodes.Status202Accepted, response.StatusCode);
        Assert.NotNull(response.Result);
        Assert.False(string.IsNullOrWhiteSpace(orderId));

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.Result);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task AddShipmentOrder_OrderTitleEmptyOrWhitespace_ReturnsAccepted(string orderTitle)
    {
        // Arrange
        var request = await IngestExpressTestDataGenerator.GenerateAddShipmentOrderRequestAsync();
        request.OrderTitle = orderTitle;

        // Act
        var response = await fixture.IngestExpressApiV1Client.AddShipmentOrderAsync(request);

        // Assert
        Assert.Equal(StatusCodes.Status202Accepted, response.StatusCode);
        var orderId = response.Result.Id;
        Assert.NotNull(orderId);

        var result =
       await fixture.RetryUntilExistsAsync(() =>
           fixture.OrderApiV1Client.GetFullOrderAsync(orderId, request.StoreId)
       );

        Assert.Equal(StatusCodes.Status202Accepted, response.StatusCode);
        Assert.NotNull(response.Result);
        Assert.False(string.IsNullOrWhiteSpace(orderId));

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.Result);
    }

    [Fact]
    public async Task AddShipmentOrder_OrderTitleWithLessCommonValidContent_ReturnsAccepted()
    {
        // Arrange
        var request = await IngestExpressTestDataGenerator.GenerateAddShipmentOrderRequestAsync();

        // Bundling into single test to minimize happy path test load with similar coverage.

        // [InlineData("Simple OrderTitle")] // Normal case
        // [InlineData("OrderTitle with unicode: café ☕")] // Unicode characters
        // [InlineData("OrderTitle with émojis 🎉🚀")] // Emojis
        // [InlineData("OrderTitle with numbers 12345")] // Numbers
        // [InlineData("OrderTitle-with-dashes-and_underscores")] // Special chars
        // [InlineData("OrderTitle (with) [brackets] {braces}")] // Various brackets
        // [InlineData("OrderTitle: with, various; punctuation! marks?")] // Punctuation

        request.OrderTitle = "café-🎉-🚀_12345! (with): [brackets], {braces}?";

        // Act
        var response = await fixture.IngestExpressApiV1Client.AddShipmentOrderAsync(request);

        // Assert
        Assert.Equal(StatusCodes.Status202Accepted, response.StatusCode);
        var orderId = response.Result.Id;
        Assert.NotNull(orderId);

        var result =
       await fixture.RetryUntilExistsAsync(() =>
           fixture.OrderApiV1Client.GetFullOrderAsync(orderId, request.StoreId)
       );

        Assert.Equal(StatusCodes.Status202Accepted, response.StatusCode);
        Assert.NotNull(response.Result);
        Assert.False(string.IsNullOrWhiteSpace(orderId));

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.Result);
    }

    [Fact]
    public async Task AddShipmentOrder_OrderPlacedDateInFuture_ReturnsAccepted()
    {
        // Arrange
        var request = await IngestExpressTestDataGenerator.GenerateAddShipmentOrderRequestAsync();
        request.OrderPlacedDate = DateTimeOffset.UtcNow.AddMonths(12); // 12 Months in the future
        request.FulfillmentStatus = FulfillmentStatus.IN_PROGRESS;
        request.OrderFulfilledDate = null;

        // Act
        var response = await fixture.IngestExpressApiV1Client.AddShipmentOrderAsync(request);

        // Assert
        var orderId = response.Result.Id;
        Assert.NotNull(orderId);

        var result =
        await fixture.RetryUntilExistsAsync(() =>
            fixture.OrderApiV1Client.GetFullOrderAsync(orderId, request.StoreId)
        );

        Assert.Equal(StatusCodes.Status202Accepted, response.StatusCode);
        Assert.NotNull(response.Result);
        Assert.False(string.IsNullOrWhiteSpace(orderId));

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.Result);
    }

    [Fact]
    public async Task AddShipmentOrder_OrderPlacedDateMaxValue_ReturnsAccepted()
    {
        // Arrange
        var request = await IngestExpressTestDataGenerator.GenerateAddShipmentOrderRequestAsync();
        request.OrderPlacedDate = DateTimeOffset.MaxValue; // Max possible date
        request.FulfillmentStatus = FulfillmentStatus.IN_PROGRESS;
        request.OrderFulfilledDate = null;

        // Act
        var response = await fixture.IngestExpressApiV1Client.AddShipmentOrderAsync(request);

        // Assert
        Assert.Equal(StatusCodes.Status202Accepted, response.StatusCode);
        var orderId = response.Result.Id;
        Assert.NotNull(orderId);

        var result =
        await fixture.RetryUntilExistsAsync(() =>
            fixture.OrderApiV1Client.GetFullOrderAsync(orderId, request.StoreId)
        );

        Assert.Equal(StatusCodes.Status202Accepted, response.StatusCode);
        Assert.NotNull(response.Result);
        Assert.False(string.IsNullOrWhiteSpace(orderId));

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.Result);
    }

    [Fact]
    public async Task AddShipmentOrder_OrderPlacedDateAfterDeliveredDate_ReturnsAccepted()
    {
        // Arrange
        var request = await IngestExpressTestDataGenerator.GenerateAddShipmentOrderRequestAsync();
        request.OrderPlacedDate = DateTimeOffset.UtcNow;
        request.OrderFulfilledDate = DateTimeOffset.UtcNow.AddHours(-1); // Delivered before sent (edge case)
        request.FulfillmentStatus = FulfillmentStatus.SUCCESS;

        // Act
        var response = await fixture.IngestExpressApiV1Client.AddShipmentOrderAsync(request);

        // Assert
        Assert.Equal(StatusCodes.Status202Accepted, response.StatusCode);
        var orderId = response.Result.Id;
        Assert.NotNull(orderId);

        var result =
        await fixture.RetryUntilExistsAsync(() =>
            fixture.OrderApiV1Client.GetFullOrderAsync(orderId, request.StoreId)
        );

        Assert.Equal(StatusCodes.Status202Accepted, response.StatusCode);
        Assert.NotNull(response.Result);
        Assert.False(string.IsNullOrWhiteSpace(orderId));

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.Result);
    }

    [Theory(Skip = "Minimizing test load for now; until we assert on the dates.")]
    [InlineData(0)]    // UTC
    [InlineData(-5)]   // EST
    [InlineData(5)]    // Some positive offset
    [InlineData(-12)]  // UTC-12 (near date line)
    [InlineData(14)]   // UTC+14 (near date line)
    public async Task AddShipmentOrder_OrderPlacedDateWithVariousTimeZones_ReturnsAccepted(int offsetHours)
    {
        // Arrange
        var request = await IngestExpressTestDataGenerator.GenerateAddShipmentOrderRequestAsync();
        var baseDate = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.FromHours(offsetHours));
        request.OrderPlacedDate = baseDate;
        request.FulfillmentStatus = FulfillmentStatus.IN_PROGRESS;
        request.OrderFulfilledDate = null;

        // Act
        var response = await fixture.IngestExpressApiV1Client.AddShipmentOrderAsync(request);

        // Assert
        Assert.Equal(StatusCodes.Status202Accepted, response.StatusCode);
        var orderId = response.Result.Id;
        Assert.NotNull(orderId);

        var result =
       await fixture.RetryUntilExistsAsync(() =>
           fixture.OrderApiV1Client.GetFullOrderAsync(orderId, request.StoreId)
       );

        Assert.Equal(StatusCodes.Status202Accepted, response.StatusCode);
        Assert.NotNull(response.Result);
        Assert.False(string.IsNullOrWhiteSpace(orderId));

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.Result);
    }

    [Fact]
    public async Task AddShipmentOrder_MissingMultipleRequiredFields_ReturnsBadRequestWithProblemDetails()
    {
        // Arrange
        var request = await IngestExpressTestDataGenerator.GenerateAddShipmentOrderRequestAsync();
        request.To.Single().Address = " ";
        request.From.Address = " ";
        request.StoreId = " ";
        request.CustomerId = " ";

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<IngestExpressApiV1ClientException<HttpValidationProblemDetails>>(async () =>
                {
                    await fixture.IngestExpressApiV1Client.AddShipmentOrderAsync(request);
                }
            );

        Assert.Equal(StatusCodes.Status400BadRequest, exception.StatusCode);
        var problemDetails = Assert.IsType<HttpValidationProblemDetails>(exception.Result);
        Assert.NotNull(problemDetails.Errors);
        Assert.Equal(4, problemDetails.Errors.Count);

        // Assert To[0].Address error
        Assert.True(problemDetails.Errors.ContainsKey("To[0].Address"));
        Assert.Equal("The Address field is required.", problemDetails.Errors["To[0].Address"].First());

        // Assert From.Address error
        Assert.True(problemDetails.Errors.ContainsKey("From.Address"));
        Assert.Equal("The Address field is required.", problemDetails.Errors["From.Address"].First());

        // Assert StoreId error
        Assert.True(problemDetails.Errors.ContainsKey(nameof(request.StoreId)));
        Assert.Equal("The StoreId field is required.", problemDetails.Errors[nameof(request.StoreId)].First());

        // Assert CustomerId error
        Assert.True(problemDetails.Errors.ContainsKey(nameof(request.CustomerId)));
        Assert.Equal("The CustomerId field is required.", problemDetails.Errors[nameof(request.CustomerId)].First());
    }

    [Fact]
    public async Task AddShipmentOrder_MultipleInvalidEnumAttributes_ReturnsBadRequest()
    {
        // Arrange
        var request = await IngestExpressTestDataGenerator.GenerateAddShipmentOrderRequestAsync();
        request.OrderFlow = (OrderHub.IntegrationTests.Clients.IngestExpressApi.V1.Contracts.OrderFlowType)999;
        request.Merchant.Name = (MerchantName)999;
        request.FulfillmentStatus = (FulfillmentStatus)999;

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<IngestExpressApiV1ClientException<HttpValidationProblemDetails>>(async () =>
                {
                    await fixture.IngestExpressApiV1Client.AddShipmentOrderAsync(request);
                }
            );

        Assert.Equal(StatusCodes.Status400BadRequest, exception.StatusCode);
        var problemDetails = Assert.IsType<HttpValidationProblemDetails>(exception.Result);
        Assert.NotNull(problemDetails.Errors);
        var errors = problemDetails.Errors;
        Assert.Equal(2, errors.Count);
        Assert.Equal("request", errors.Keys.First());
        Assert.Equal("The request field is required.", errors.Values.First().First());

        //only the first enum error in the request is captured
        Assert.Equal("$.orderFlow", errors.Keys.Last());
        Assert.StartsWith(
            "The JSON value could not be converted to OrderHub.Contracts.Common.Enums.OrderFlowType. Path: $.orderFlow",
            errors.Values.Last().Last()
        );
    }

    #region Idempotency Tests

    [Fact]
    public async Task AddShipmentOrder_DuplicateSourceOrderId_Returns409WithExistingId()
    {
        // Arrange
        var request = await IngestExpressTestDataGenerator.GenerateAddShipmentOrderRequestAsync();
        request.Merchant.OrderId = $"TXN-SHIP-DUP-{Guid.NewGuid():N}";

        // Act - First call
        var firstResponse = await fixture.IngestExpressApiV1Client.AddShipmentOrderAsync(request);

        // Assert first call
        Assert.Equal(StatusCodes.Status202Accepted, firstResponse.StatusCode);
        var firstOrderId = firstResponse.Result.Id;
        Assert.NotNull(firstOrderId);

        // Verify first call persisted to MongoDB
        var firstResult = await fixture.RetryUntilExistsAsync(() =>
            fixture.OrderApiV1Client.GetFullOrderAsync(firstOrderId, request.StoreId)
        );
        Assert.Equal(StatusCodes.Status200OK, firstResult.StatusCode);

        // Act & Assert - Second call with same Merchant.OrderId should throw 409 exception
        var exception = await Assert.ThrowsAsync<IngestExpressApiV1ClientException<DuplicateOrderResponse>>(async () =>
        {
            await fixture.IngestExpressApiV1Client.AddShipmentOrderAsync(request);
        });

        // Assert exception details
        Assert.Equal(StatusCodes.Status409Conflict, exception.StatusCode);
        Assert.NotNull(exception.Result);
        var secondOrderId = exception.Result.Id;
        Assert.Equal(firstOrderId, secondOrderId);

        // Verify only ONE MongoDB document exists
        var finalResult = await fixture.OrderApiV1Client.GetFullOrderAsync(firstOrderId, request.StoreId);
        Assert.Equal(StatusCodes.Status200OK, finalResult.StatusCode);
    }

    [Fact]
    public async Task AddShipmentOrder_DifferentSourceOrderIds_BothAccepted()
    {
        // Arrange
        var request1 = await IngestExpressTestDataGenerator.GenerateAddShipmentOrderRequestAsync();
        request1.Merchant.OrderId = $"TXN-SHIP-001-{Guid.NewGuid():N}";

        var request2 = await IngestExpressTestDataGenerator.GenerateAddShipmentOrderRequestAsync();
        request2.Merchant.OrderId = $"TXN-SHIP-002-{Guid.NewGuid():N}";
        request2.StoreId = request1.StoreId; // Same StoreId for easier assertion

        // Act
        var response1 = await fixture.IngestExpressApiV1Client.AddShipmentOrderAsync(request1);
        var response2 = await fixture.IngestExpressApiV1Client.AddShipmentOrderAsync(request2);

        // Assert both return 202 Accepted
        Assert.Equal(StatusCodes.Status202Accepted, response1.StatusCode);
        Assert.Equal(StatusCodes.Status202Accepted, response2.StatusCode);

        var orderId1 = response1.Result.Id;
        var orderId2 = response2.Result.Id;

        Assert.NotNull(orderId1);
        Assert.NotNull(orderId2);
        Assert.NotEqual(orderId1, orderId2);

        // Verify both persist to MongoDB
        var result1 = await fixture.RetryUntilExistsAsync(() =>
            fixture.OrderApiV1Client.GetFullOrderAsync(orderId1, request1.StoreId)
        );
        var result2 = await fixture.RetryUntilExistsAsync(() =>
            fixture.OrderApiV1Client.GetFullOrderAsync(orderId2, request2.StoreId)
        );

        Assert.Equal(StatusCodes.Status200OK, result1.StatusCode);
        Assert.Equal(StatusCodes.Status200OK, result2.StatusCode);
    }

    [Fact]
    public async Task AddShipmentOrder_SameSourceId_DifferentPriority_BothAccepted()
    {
        // Arrange - EXPRESS ORDER
        var txnRequest = await IngestExpressTestDataGenerator.GenerateAddShipmentOrderRequestAsync();
        var crossPriId = $"CROSS-PRI-SHIP-{Guid.NewGuid():N}";
        txnRequest.Merchant.OrderId = crossPriId;

        // Arrange - STANDARD ORDER with same source ID (using IngestStandard API)
        var autoRequest = new OrderHub.IntegrationTests.Clients.IngestStandardApi.V1.Contracts.AddShipmentOrderRequest
        {
            StoreId = txnRequest.StoreId,
            CustomerId = txnRequest.CustomerId,
            OrderFlow = (OrderHub.IntegrationTests.Clients.IngestStandardApi.V1.Contracts.OrderFlowType)txnRequest.OrderFlow,
            OrderPlacedDate = txnRequest.OrderPlacedDate,
            OrderFulfilledDate = txnRequest.OrderFulfilledDate,
            Merchant = new OrderHub.IntegrationTests.Clients.IngestStandardApi.V1.Contracts.Merchant
            {
                Name = (OrderHub.IntegrationTests.Clients.IngestStandardApi.V1.Contracts.MerchantName)txnRequest.Merchant.Name,
                OrderId = crossPriId,
                SourceApplication = txnRequest.Merchant.SourceApplication
            },
            FulfillmentStatus = (OrderHub.IntegrationTests.Clients.IngestStandardApi.V1.Contracts.FulfillmentStatus)txnRequest.FulfillmentStatus,
            To = txnRequest.To.Select(t => new OrderHub.IntegrationTests.Clients.IngestStandardApi.V1.Contracts.AddressInfo
            {
                Address = t.Address,
                Name = t.Name
            }).ToList(),
            From = new OrderHub.IntegrationTests.Clients.IngestStandardApi.V1.Contracts.AddressInfo
            {
                Address = txnRequest.From.Address,
                Name = txnRequest.From.Name
            },
            OrderTitle = txnRequest.OrderTitle,
            Content = txnRequest.Content
        };

        // Act
        var txnResponse = await fixture.IngestExpressApiV1Client.AddShipmentOrderAsync(txnRequest);
        var autoResponse = await fixture.IngestStandardApiV1Client.AddShipmentOrderAsync(autoRequest);

        // Assert both return 202 Accepted (no conflict between different priorities)
        Assert.Equal(StatusCodes.Status202Accepted, txnResponse.StatusCode);
        Assert.Equal(StatusCodes.Status202Accepted, autoResponse.StatusCode);

        var txnOrderId = txnResponse.Result.Id;
        var autoOrderId = autoResponse.Result.Id;

        Assert.NotNull(txnOrderId);
        Assert.NotNull(autoOrderId);
        Assert.NotEqual(txnOrderId, autoOrderId);

        // Verify both persist to MongoDB
        var txnResult = await fixture.RetryUntilExistsAsync(() =>
            fixture.OrderApiV1Client.GetFullOrderAsync(txnOrderId, txnRequest.StoreId)
        );
        var autoResult = await fixture.RetryUntilExistsAsync(() =>
            fixture.OrderApiV1Client.GetFullOrderAsync(autoOrderId, autoRequest.StoreId)
        );

        Assert.Equal(StatusCodes.Status200OK, txnResult.StatusCode);
        Assert.Equal(StatusCodes.Status200OK, autoResult.StatusCode);
    }

    #endregion
}
