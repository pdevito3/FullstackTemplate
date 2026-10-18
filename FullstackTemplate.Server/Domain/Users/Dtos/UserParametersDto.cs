namespace FullstackTemplate.Server.Domain.Users.Dtos;

using Microsoft.AspNetCore.Mvc;
using Resources;

public sealed class UserParametersDto : BasePaginationParameters
{
    [FromQuery(Name = "filters")]
    public string? Filters { get; set; }

    [FromQuery(Name = "sortOrder")]
    public string? SortOrder { get; set; }
}
