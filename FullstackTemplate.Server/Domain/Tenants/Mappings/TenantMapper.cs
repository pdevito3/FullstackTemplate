namespace FullstackTemplate.Server.Domain.Tenants.Mappings;

using Dtos;
using Models;
using Riok.Mapperly.Abstractions;
using Users;

[Mapper]
public static partial class TenantMapper
{
    /// <summary>The DTO with the actions that <paramref name="caller"/> can send for the tenant.</summary>
    public static TenantDto ToTenantDto(this Tenant tenant, Caller caller)
        => tenant.ToTenantDtoWithoutActions() with { AllowedActions = NamesOf(tenant.AllowedActions(caller)) };

    /// <summary>Gives each row of a list the actions that <paramref name="caller"/> can send for it.</summary>
    public static void AddAllowedActions(this List<TenantDto> tenants, Caller caller)
    {
        var actions = NamesOf(TenantAction.AllowedFor(caller));
        for (var i = 0; i < tenants.Count; i++)
            tenants[i] = tenants[i] with { AllowedActions = actions };
    }

    // It takes the configuration of ToTenantDtoWithoutActions. The list handler adds the actions after the page loads.
    public static partial IQueryable<TenantDto> ToTenantDtoQueryable(this IQueryable<Tenant> queryable);

    [MapperIgnoreTarget(nameof(TenantForCreation.Id))]
    public static partial TenantForCreation ToTenantForCreation(this TenantForCreationDto dto);

    public static partial TenantForUpdate ToTenantForUpdate(this TenantForUpdateDto dto);

    [MapperIgnoreSource(nameof(Tenant.LastModifiedOn))]
    [MapperIgnoreSource(nameof(Tenant.IsDeleted))]
    [MapperIgnoreSource(nameof(Tenant.DomainEvents))]
    [MapperIgnoreTarget(nameof(TenantDto.AllowedActions))]
    private static partial TenantDto ToTenantDtoWithoutActions(this Tenant tenant);

    private static List<string> NamesOf(IEnumerable<TenantAction> actions) => actions.Select(a => a.Value).ToList();
}
