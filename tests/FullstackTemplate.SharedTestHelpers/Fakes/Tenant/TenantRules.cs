namespace FullstackTemplate.SharedTestHelpers.Fakes.Tenant;

using Bogus;

/// <summary>
/// The one definition of a realistic tenant. Each method is named for the property that it fills.
/// The shape fakers and the entity graph blueprint use these methods, so a change here reaches all of them.
/// </summary>
public static class TenantRules
{
    // Tenant names are unique in the database, and the company names repeat often. The suffix stops two
    // tests that run at the same time from making the same name.
    public static string Name(Faker f) => $"{f.Company.CompanyName()} {Guid.NewGuid().ToString("N")[..8]}";
}
