using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace OrderHub.Contracts;

public class PaginatedResponse<T>
{
    private static string FormatUrl(string urlBase, int page, int pageSize)
    {
        var separator = urlBase.Contains('?') ? '&' : '?';
        return $"{urlBase}{separator}page={page}&pageSize={pageSize}";
    }

    [SetsRequiredMembers]
    public PaginatedResponse(string urlBase, int page, int pageSize, long count, List<T> items)
    {
        Href = FormatUrl(urlBase, page, pageSize);
        Count = count;
        Items = items;
        Page = page;
        PageSize = pageSize;

        if (page * pageSize < count)
        {
            Next = FormatUrl(urlBase, page + 1, pageSize);
        }

        if (page > 1)
        {
            First = FormatUrl(urlBase, 1, pageSize);
            Previous = FormatUrl(urlBase, page - 1, pageSize);
        }
    }

    /// <summary>
    /// HREF pointing to the current page of results.
    /// </summary>
    [Required]
    public required string Href { get; init; }

    /// <summary>
    /// The total number of items for this resource. This field can be used to derive total page numbers for a given page size.
    /// </summary>
    public required long Count { get; init; }

    /// <summary>
    /// A list of items on the current page.
    /// </summary>
    [Required]
    public required List<T> Items { get; init; }

    /// <summary>
    /// HREF pointing to the next page of results.
    /// </summary>
    /// <remarks>This string is null when the last page is the page being retrieved</remarks>
    /// <example>null</example>
    public string? Next { get; init; }

    /// <summary>
    /// HREF pointing to the previous page of results.
    /// </summary>
    /// <remarks>This string is null when the first page is the page being retrieved</remarks>
    /// <example>null</example>
    public string? Previous { get; init; }

    /// <summary>
    /// HREF pointing to the first page of results.
    /// </summary>
    /// <remarks>This string is null when the first page is the page being retrieved</remarks>
    /// <example>null</example>
    public string? First { get; init; }

    /// <summary>
    /// The Page to be returned.
    /// </summary>
    /// <example>1</example>
    [Range(1, int.MaxValue)]
    public required int Page { get; init; }

    /// <summary>
    /// The number of items to be returned per page.
    /// </summary>
    /// <example>25</example>
    [Range(1, 500)]
    public required int PageSize { get; init; }
}
