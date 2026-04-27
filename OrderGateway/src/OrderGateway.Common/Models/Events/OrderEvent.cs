using Destructurama.Attributed;
using System.Text.Json.Serialization;
using OrderGateway.Common.Helpers;

namespace OrderGateway.Common.Models.Events;

public class OrderEvent : IOrderEvent
{
    public string Type { get; set; } = string.Empty;

    public string SubType { get; set; } = string.Empty;

    [LogMasked]
    public string Description { get; set; } = string.Empty;

    public string? CreatedOn { get; set; }

    [LogMasked]
    public Dictionary<string, string>? Metadata { get; set; }

    public string? VideoMedia => GetMetadataValue("VideoMedia");

    public string? CorrelationId => GetMetadataValue("CorrelationId");

    public UserContactType UserContactType => UserContactType.Address;

    public OrderDirection Direction => GetDirection();

    public string Contact => GetContact();

    public int CustomerId => GetIntPropertyFromMetadata("CustomerId");

    public int UserId => GetIntPropertyFromMetadata("UserId");

    public int StoreId => GetIntPropertyFromMetadata("StoreId");

    public int ApproximateReceiveCount { get; set; }

    // Lazy-parsed from metadata; settable for testing or manual override.
    private List<ContactAddress>? _recipientAddresses;
    private bool _recipientAddressesParsed;

    [JsonIgnore]
    public List<ContactAddress>? RecipientAddresses
    {
        get
        {
            if (!_recipientAddressesParsed)
            {
                var val = GetMetadataValue("RecipientAddress");
                _recipientAddresses = !string.IsNullOrWhiteSpace(val)
                    ? AddressParser.ParseAddressList(val)
                    : null;
                _recipientAddressesParsed = true;
            }
            return _recipientAddresses;
        }
        set
        {
            _recipientAddresses = value;
            _recipientAddressesParsed = true;
        }
    }

    private ContactAddress? _senderAddress;
    private bool _senderAddressParsed;

    [JsonIgnore]
    public ContactAddress? SenderAddress
    {
        get
        {
            if (!_senderAddressParsed)
            {
                var val = GetMetadataValue("SenderAddress");
                _senderAddress = !string.IsNullOrWhiteSpace(val)
                    ? AddressParser.ParseAddress(val)
                    : null;
                _senderAddressParsed = true;
            }
            return _senderAddress;
        }
        set
        {
            _senderAddress = value;
            _senderAddressParsed = true;
        }
    }

    // Classification from metadata (batch, scheduled, deferred, etc.)
    public string? Classification => GetMetadataValue("Classification");

    // Derived priority classification for order events
    public bool IsStandardPriority =>
        Classification != null &&
        (Classification.Equals("batch", StringComparison.OrdinalIgnoreCase)
            || Classification.Equals("scheduled", StringComparison.OrdinalIgnoreCase)
            || Classification.Equals("deferred", StringComparison.OrdinalIgnoreCase));

    /// <summary>Pairs a human-readable error message with an optional telemetry counter name.</summary>
    private record struct ValidationFinding(string? Message, string? CounterName);

    private IReadOnlyList<ValidationFinding>? _validationFindings;

    public bool IsValid()
    {
        _validationFindings ??= ComputeValidationFindings();
        return !_validationFindings.Any(f => f.Message != null);
    }

    public IReadOnlyList<string> GetValidationErrors()
    {
        _validationFindings ??= ComputeValidationFindings();
        return _validationFindings
            .Where(f => f.Message != null)
            .Select(f => f.Message!)
            .ToList();
    }

    /// <summary>
    /// Emits NewRelic counters for every validation finding that carries a counter name.
    /// Call this from the pipeline step — not from the model itself — to keep telemetry
    /// out of domain logic.
    /// </summary>
    public void EmitValidationCounters()
    {
        _validationFindings ??= ComputeValidationFindings();
        foreach (var finding in _validationFindings)
        {
            if (finding.CounterName != null)
            {
                NewRelic.Api.Agent.NewRelic.IncrementCounter(finding.CounterName);
            }
        }
    }

    /// <summary>
    /// Pure computation — returns validation findings without side effects.
    /// No telemetry emission, no logging, no property mutation.
    /// Address parsing is handled by the lazy <see cref="RecipientAddresses"/>
    /// and <see cref="SenderAddress"/> property getters.
    /// </summary>
    private List<ValidationFinding> ComputeValidationFindings()
    {
        var findings = new List<ValidationFinding>();

        // Validate direct property Type (always required regardless of Metadata)
        if (string.IsNullOrWhiteSpace(Type))
        {
            findings.Add(new("Type is missing", "Custom/Order/Validation/NoType"));
        }

        // Validate Metadata-dependent fields
        if (Metadata == null)
        {
            findings.Add(new("Metadata is null", "Custom/Order/Validation/NullOrderEventMetaData"));
        }
        else
        {
            var storeIdStr = GetMetadataValue("StoreId");
            if (
                string.IsNullOrWhiteSpace(storeIdStr)
                || !int.TryParse(storeIdStr, out var storeId)
                || storeId <= 0
            )
            {
                findings.Add(new("StoreId is missing or invalid", "Custom/Order/Validation/NoStoreId"));
            }

            var contactIdStr = GetMetadataValue("CustomerId");
            if (
                string.IsNullOrWhiteSpace(contactIdStr)
                || !int.TryParse(contactIdStr, out var contactId)
                || contactId == 0
            )
            {
                findings.Add(new("CustomerId is missing or invalid", "Custom/Order/Validation/NoCustomerId"));
            }

            if (string.IsNullOrWhiteSpace(GetMetadataValue("OrderReferenceId")))
            {
                findings.Add(new("OrderReferenceId is missing", "Custom/Order/Validation/NoOrderReferenceId"));
            }

            if (Classification != null && Classification.Equals("alert", StringComparison.OrdinalIgnoreCase))
            {
                findings.Add(new("Classification is 'alert' which is not allowed", "Custom/Order/Validation/AlertClassification"));
            }

            var recipientValue = GetMetadataValue("RecipientAddress");
            if (string.IsNullOrWhiteSpace(recipientValue))
            {
                findings.Add(new("RecipientAddress is missing", "Custom/Order/Validation/NoRecipientAddress"));
            }
            else if (RecipientAddresses == null || RecipientAddresses.Count == 0)
            {
                findings.Add(new("RecipientAddress has invalid format", "Custom/Order/Validation/InvalidRecipientAddress"));
            }

            var senderValue = GetMetadataValue("SenderAddress");
            if (string.IsNullOrWhiteSpace(senderValue))
            {
                findings.Add(new("SenderAddress is missing", "Custom/Order/Validation/NoSenderAddress"));
            }
            else if (SenderAddress == null)
            {
                findings.Add(new("SenderAddress has invalid format", "Custom/Order/Validation/InvalidSenderAddress"));
            }

            if (string.IsNullOrWhiteSpace(GetMetadataValue("OrderFlowType")))
            {
                findings.Add(new("OrderFlowType (direction) is missing", "Custom/Order/Validation/NoDirection"));
            }

            // Informational counter only — no validation error.
            if (DateTime.TryParse(CreatedOn, out var timestamp) && timestamp == DateTime.MinValue)
            {
                findings.Add(new(null, "Custom/Order/Validation/DefaultTimestamp"));
            }
        }

        return findings;
    }

    private OrderDirection GetDirection()
    {
        if (Metadata == null)
        {
            return OrderDirection.UNKNOWN;
        }

        var orderFlowType = GetMetadataValue("OrderFlowType");
        if (orderFlowType?.Trim().Contains("outbound", StringComparison.CurrentCultureIgnoreCase) == true)
        {
            return OrderDirection.OUTGOING;
        }

        return OrderDirection.INCOMING;
    }

    private string GetContact()
    {
        if (Metadata == null)
        {
            return string.Empty;
        }

        return GetMetadataValue(Direction == OrderDirection.OUTGOING ? "SenderAddress" : "RecipientAddress") ?? string.Empty;
    }

    public string? GetMetadataValue(string key)
    {
        return Metadata?.FirstOrDefault(e => e.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Value;
    }

    private int GetIntPropertyFromMetadata(string propertyName)
    {
        var propertyValue = GetMetadataValue(propertyName);
        if (int.TryParse(propertyValue, out var intValue))
        {
            return intValue;
        }

        return 0;
    }
}
