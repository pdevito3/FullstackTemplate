namespace FullstackTemplate.Server.Domain.Agents.Dtos;

using Resources;

public sealed class AgentParametersDto : BasePaginationParameters
{
    public string? Filters { get; set; }

    public string? SortOrder { get; set; }

    /// <summary>
    /// Makes the parameters from the values of a caller. A null value keeps the default. An empty filter or sort
    /// order means none.
    /// </summary>
    public static AgentParametersDto From(int? pageNumber, int? pageSize, string? filters, string? sortOrder)
    {
        var parameters = new AgentParametersDto
        {
            Filters = string.IsNullOrEmpty(filters) ? null : filters,
            SortOrder = string.IsNullOrEmpty(sortOrder) ? null : sortOrder,
        };
        parameters.SetPage(pageNumber, pageSize);
        return parameters;
    }
}
