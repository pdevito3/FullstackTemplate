namespace FullstackTemplate.SharedTestHelpers.Fakes.Tenant;

using Bogus;

/// <summary>
/// The one definition of a realistic tenant. Each method is named for the property that it fills.
/// The shape fakers and the entity graph blueprint use these methods, so a change here reaches all of them.
/// </summary>
public static class TenantRules
{
    public static string Name(Faker f) => f.Company.CompanyName();
}
