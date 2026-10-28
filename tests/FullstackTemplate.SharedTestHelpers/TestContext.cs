namespace FullstackTemplate.SharedTestHelpers;

using FullstackTemplate.Server.Domain.Tenants;

/// <summary>
/// Provides shared test context that fakers can access to use real database entities.
/// Integration tests should override DefaultTenantId after creating a real tenant.
/// </summary>
public static class TestContext
{
    /// <summary>
    /// The default tenant ID to use for entities that require a tenant.
    /// Initialized with a placeholder tenant ID for unit tests.
    /// Integration tests override this after creating a real tenant in the database.
    /// </summary>
    public static string DefaultTenantId { get; set; } = $"{Tenant.IdPrefix}_{new string('1', 32)}";
}
