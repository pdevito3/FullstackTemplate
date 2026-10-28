namespace FullstackTemplate.Server.Examples.Venues.Dtos;

using Resources;

public sealed class VenueParametersDto : BasePaginationParameters
{
    public string? Filters { get; set; }

    public string? SortOrder { get; set; }

    /// <summary>
    /// Makes the parameters from the values of a caller. A null value keeps the default. An empty filter or sort
    /// order means none.
    /// </summary>
    public static VenueParametersDto From(int? pageNumber, int? pageSize, string? filters, string? sortOrder)
    {
        var parameters = new VenueParametersDto
        {
            Filters = string.IsNullOrEmpty(filters) ? null : filters,
            SortOrder = string.IsNullOrEmpty(sortOrder) ? null : sortOrder,
        };
        parameters.SetPage(pageNumber, pageSize);
        return parameters;
    }
}
