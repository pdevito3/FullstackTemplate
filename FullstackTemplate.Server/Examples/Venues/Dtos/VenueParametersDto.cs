namespace FullstackTemplate.Server.Examples.Venues.Dtos;

using Resources;

public sealed class VenueParametersDto : BasePaginationParameters
{
    public const int SearchMaxLength = Venue.NameMaxLength;

    public string? Filters { get; set; }

    public string? SortOrder { get; set; }

    public string? Search { get; set; }

    public static VenueParametersDto From(
        int? pageNumber, int? pageSize, string? filters, string? sortOrder, string? search)
    {
        var parameters = new VenueParametersDto
        {
            Filters = string.IsNullOrEmpty(filters) ? null : filters,
            SortOrder = string.IsNullOrEmpty(sortOrder) ? null : sortOrder,
            Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
        };
        parameters.SetPage(pageNumber, pageSize);
        return parameters;
    }
}
