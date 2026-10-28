namespace FullstackTemplate.Server.Examples.Workshops.Dtos;

using Resources;

public sealed class WorkshopParametersDto : BasePaginationParameters
{
    public const int SearchMaxLength = Workshop.TitleMaxLength;

    public string? Filters { get; set; }

    public string? SortOrder { get; set; }

    /// <summary>A text that the title contains, with no regard to case. Null means no search.</summary>
    public string? Search { get; set; }

    /// <summary>
    /// Makes the parameters from the values of a caller. A null value keeps the default. An empty filter or sort
    /// order means none. A search that is empty after the trim means none.
    /// </summary>
    public static WorkshopParametersDto From(
        int? pageNumber, int? pageSize, string? filters, string? sortOrder, string? search)
    {
        var parameters = new WorkshopParametersDto
        {
            Filters = string.IsNullOrEmpty(filters) ? null : filters,
            SortOrder = string.IsNullOrEmpty(sortOrder) ? null : sortOrder,
            Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
        };
        parameters.SetPage(pageNumber, pageSize);
        return parameters;
    }
}
