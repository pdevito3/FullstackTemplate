namespace FullstackTemplate.Server.Domain.Tenants.Mappings;

using Dtos;
using Models;
using Riok.Mapperly.Abstractions;

[Mapper]
public static partial class TenantMapper
{
    [MapperIgnoreSource(nameof(Tenant.CreatedBy))]
    [MapperIgnoreSource(nameof(Tenant.LastModifiedBy))]
    [MapperIgnoreSource(nameof(Tenant.CreatedOn))]
    [MapperIgnoreSource(nameof(Tenant.LastModifiedOn))]
    [MapperIgnoreSource(nameof(Tenant.IsDeleted))]
    [MapperIgnoreSource(nameof(Tenant.DomainEvents))]
    public static partial TenantDto ToTenantDto(this Tenant tenant);

    public static partial IQueryable<TenantDto> ToTenantDtoQueryable(this IQueryable<Tenant> queryable);

    public static partial TenantForCreation ToTenantForCreation(this TenantForCreationDto dto);

    public static partial TenantForUpdate ToTenantForUpdate(this TenantForUpdateDto dto);
}
