namespace FullstackTemplate.Server.Domain.Tenants.Grpc.V1;

using Dtos;
using Resources;

/// <summary>Maps the tenant DTOs to the gRPC v1 messages. More than one RPC uses each map.</summary>
public static class TenantGrpcMappings
{
    public static Tenant ToMessage(this TenantDto dto) => new() { Id = dto.Id, Name = dto.Name };

    public static Pagination ToPaginationMessage(this IPagedList pagedList) => new()
    {
        PageNumber = pagedList.PageNumber,
        PageSize = pagedList.PageSize,
        CurrentPageSize = pagedList.CurrentPageSize,
        TotalCount = pagedList.TotalCount,
        TotalPages = pagedList.TotalPages,
        HasPrevious = pagedList.HasPrevious,
        HasNext = pagedList.HasNext,
    };
}
