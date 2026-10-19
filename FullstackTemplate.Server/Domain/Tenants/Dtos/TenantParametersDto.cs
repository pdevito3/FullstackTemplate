namespace FullstackTemplate.Server.Domain.Tenants.Dtos;

using Microsoft.AspNetCore.Mvc;
using Resources;

public sealed class TenantParametersDto : BasePaginationParameters
{
    [FromQuery(Name = "filters")]
    public string? Filters { get; set; }

    [FromQuery(Name = "sortOrder")]
    public string? SortOrder { get; set; }
}
