namespace OrderHub.Common.Models.Components;

public class OrderMetadata
{
    public required List<string> MediaIds { get; set; }

    public required int ContentLength { get; set; }

    public required int VisibleContentLength { get; set; }

    public int? PlainTextContentLength { get; set; }
}
