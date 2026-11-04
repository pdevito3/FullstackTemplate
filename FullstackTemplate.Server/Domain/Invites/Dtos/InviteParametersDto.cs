namespace FullstackTemplate.Server.Domain.Invites.Dtos;

using Resources;

public sealed class InviteParametersDto : BasePaginationParameters
{
    public string? Filters { get; set; }

    public string? SortOrder { get; set; }

    public static InviteParametersDto From(
        int? pageNumber, int? pageSize, string? filters, string? sortOrder)
    {
        var parameters = new InviteParametersDto
        {
            Filters = string.IsNullOrEmpty(filters) ? null : filters,
            SortOrder = string.IsNullOrEmpty(sortOrder) ? null : sortOrder,
        };
        parameters.SetPage(pageNumber, pageSize);
        return parameters;
    }
}
