namespace FullstackTemplate.SharedTestHelpers.Fakes.Agent;

using Bogus;
using FullstackTemplate.Server.Domain.Agents;

/// <summary>
/// The one definition of a realistic agent. Each method is named for the property that it fills.
/// The shape fakers and the entity graph blueprint use these methods, so a change here reaches all of them.
/// </summary>
public static class AgentRules
{
    public static string Name(Faker f) => $"{f.Commerce.ProductName()} assistant";

    // A machine client ID is unique in all tenants, and tests that run at the same time share the database.
    public static string ClientId(Faker f) => $"{f.Internet.DomainWord()}-{f.Random.Guid():N}";

    // A tenant admin can add only a delegated agent, so that is the default.
    public static string Mode(Faker _) => AgentMode.Delegated().Value;
}
