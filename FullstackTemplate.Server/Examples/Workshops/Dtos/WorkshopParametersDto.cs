namespace FullstackTemplate.Server.Examples.Workshops.Dtos;

using Resources;

public sealed class WorkshopParametersDto : BasePaginationParameters
{
    public string? Filters { get; set; }

    public string? SortOrder { get; set; }

    public static WorkshopParametersDto From(
        int? pageNumber, int? pageSize, string? filters, string? sortOrder)
    {
        var parameters = new WorkshopParametersDto
        {
            Filters = string.IsNullOrEmpty(filters) ? null : filters,
            SortOrder = string.IsNullOrEmpty(sortOrder) ? null : sortOrder,
        };
        parameters.SetPage(pageNumber, pageSize);
        return parameters;
    }
}
