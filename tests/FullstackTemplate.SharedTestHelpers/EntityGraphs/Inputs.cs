namespace FullstackTemplate.SharedTestHelpers.EntityGraphs;

// Inputs are settable construction data for a blueprint. A test sets only the properties under test.
// A property that is still null after the customizer gets a realistic value from a Derive in Blueprints.
// An explicit value, also an empty string, is kept. Use that to arrange invalid data.

public sealed class TenantInput
{
    /// <summary>A <c>ten_</c> ID. Null by default, so the entity makes the ID. Set it to give a fixed ID.</summary>
    public string? Id { get; set; }

    public string? Name { get; set; }
}

public sealed class UserInput
{
    /// <summary>A <c>usr_</c> ID. Null by default, so the entity makes the ID. Set it to give a fixed ID.</summary>
    public string? Id { get; set; }

    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Identifier { get; set; }
    public string? Email { get; set; }
    public string? Username { get; set; }

    /// <summary>A <c>UserRole</c> name, for example "Admin" or "User". Role defaults give the permissions.</summary>
    public string? Role { get; set; }
}
