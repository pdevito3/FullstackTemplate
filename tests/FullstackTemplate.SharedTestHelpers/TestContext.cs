namespace FullstackTemplate.SharedTestHelpers;

/// <summary>
/// Provides shared test context that fakers can access to use real database entities.
/// Integration tests should override DefaultTenantId after creating a real tenant.
/// </summary>
public static class TestContext
{
    /// <summary>
    /// The default tenant ID to use for entities that require a tenant.
    /// Initialized with a placeholder GUID for unit tests.
    /// Integration tests override this after creating a real tenant in the database.
    /// </summary>
    public static Guid DefaultTenantId { get; set; } = Guid.Parse("11111111-1111-1111-1111-111111111111");
}
