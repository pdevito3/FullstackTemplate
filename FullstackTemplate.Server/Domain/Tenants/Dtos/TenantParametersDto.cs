namespace FullstackTemplate.Server.Domain.Tenants.Dtos;

using Resources;

public sealed class TenantParametersDto : BasePaginationParameters
{
    public string? Filters { get; set; }
    public string? SortOrder { get; set; }
}
