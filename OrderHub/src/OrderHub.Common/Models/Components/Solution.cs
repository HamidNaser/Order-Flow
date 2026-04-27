using System.ComponentModel.DataAnnotations;

namespace OrderHub.Common.Models.Components;

public class Platform
{
    [Required]
    public required PlatformId Id { get; set; }

    public string? OperationId { get; set; }

    public string? CustomerId { get; set; }

    public string? CustomerName { get; set; }

    public string? AgentId { get; set; }

    public string? AgentName { get; set; }

    public string? TrackingId { get; set; }
}
