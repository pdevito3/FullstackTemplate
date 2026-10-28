namespace FullstackTemplate.IntegrationTests.Databases;

using FullstackTemplate.Server.Domain.Agents.Features;
using FullstackTemplate.Server.Domain.Tenants.Dtos;
using FullstackTemplate.Server.Domain.Tenants.Features;
using FullstackTemplate.Server.Domain.Users.Features;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Fakes.Agent;
using FullstackTemplate.SharedTestHelpers.Fakes.Tenant;
using FullstackTemplate.SharedTestHelpers.Fakes.User;
using FullstackTemplate.SharedTestHelpers.Utilities;
using Shouldly;

/// <summary>
/// Two requests can pass the duplicate check of a handler at the same time. Then the unique index stops the second
/// save. The loser must get the same message as a request that the check stops.
/// </summary>
public class UniqueCheckRaceTests : TestBase
{
    [Test]
    public async Task loser_of_a_tenant_add_race_gets_the_tenant_name_message()
    {
        // Arrange
        var pause = PauseAtQuery.After("tenants.add.name_in_use");
        await using var services = pause.BuildServices();
        var underTest = new TestingServiceScope(services);
        var rival = new TestingServiceScope();
        underTest.SetMachineClient(TestFixture.TrustedMachineClientId);
        rival.SetMachineClient(TestFixture.TrustedMachineClientId);
        var dto = new FakeTenantForCreationDto().Generate();

        // Act
        var add = underTest.SendAsync(new AddTenant.Command(dto));
        await pause.ReachedAsync();
        await rival.SendAsync(new AddTenant.Command(dto));
        pause.Release();

        // Assert
        await ShouldBeConflictAsync(add, "name", "A tenant with this name already exists.");
    }

    [Test]
    public async Task loser_of_a_tenant_rename_race_gets_the_tenant_name_message()
    {
        // Arrange
        var pause = PauseAtQuery.After("tenants.update.name_in_use");
        await using var services = pause.BuildServices();
        await new TestingServiceScope().StartGraph(out _)
            .AddTenant(out var tenant)
            .AddUser(out var admin, u => u.Role = "Admin")
            .BuildAsync();
        var underTest = new TestingServiceScope(services);
        var rival = new TestingServiceScope();
        underTest.SetUserWithDefaults(identifier: admin.Identifier);
        rival.SetMachineClient(TestFixture.TrustedMachineClientId);
        var loaded = await underTest.SendAsync(new GetTenant.Query(tenant.Id));
        var name = $"Tenant {Guid.NewGuid():N}";

        // Act
        var update = underTest.SendAsync(new UpdateTenant.Command(tenant.Id,
            new TenantForUpdateDto { Name = name, Version = loaded.Version }));
        await pause.ReachedAsync();
        await rival.SendAsync(new AddTenant.Command(new FakeTenantForCreationDto().Generate() with { Name = name }));
        pause.Release();

        // Assert
        await ShouldBeConflictAsync(update, "name", "A tenant with this name already exists.");
    }

    [Test]
    public async Task loser_of_a_user_add_race_gets_the_identifier_message()
    {
        // Arrange
        var pause = PauseAtQuery.After("users.add.identifier_in_use");
        await using var services = pause.BuildServices();
        var underTest = new TestingServiceScope(services);
        var rival = new TestingServiceScope();
        var dto = new FakeUserForCreationDto().Generate();

        // Act
        var add = underTest.SendAsync(new AddUser.Command(dto));
        await pause.ReachedAsync();
        await rival.SendAsync(new AddUser.Command(dto));
        pause.Release();

        // Assert
        await ShouldBeConflictAsync(add, "identifier", "A user with this identifier already exists.");
    }

    [Test]
    public async Task loser_of_an_initiate_race_gets_the_identifier_message()
    {
        // Arrange
        var pause = PauseAtQuery.After("users.initiate.identifier_in_use");
        await using var services = pause.BuildServices();
        var dto = new FakeInitiateUserAndTenantDto().Generate();
        var underTest = new TestingServiceScope(services);
        var rival = new TestingServiceScope();
        underTest.SetUserWithDefaults(identifier: dto.Identifier);
        rival.SetUserWithDefaults(identifier: dto.Identifier);

        // Act
        var initiate = underTest.SendAsync(new InitiateUserAndTenant.Command(dto));
        await pause.ReachedAsync();
        await rival.SendAsync(new InitiateUserAndTenant.Command(dto with { TenantName = $"Tenant {Guid.NewGuid():N}" }));
        pause.Release();

        // Assert
        await ShouldBeConflictAsync(initiate, "identifier", "A user with this identifier already exists.");
    }

    [Test]
    public async Task loser_of_an_agent_add_race_gets_the_client_id_message()
    {
        // Arrange
        var pause = PauseAtQuery.After("agents.add.client_id_in_use");
        await using var services = pause.BuildServices();
        await new TestingServiceScope().StartGraph(out _)
            .AddTenant(out _)
            .AddUser(out var admin, u => u.Role = "Admin")
            .BuildAsync();
        var underTest = new TestingServiceScope(services);
        var rival = new TestingServiceScope();
        underTest.SetUserWithDefaults(identifier: admin.Identifier);
        rival.SetUserWithDefaults(identifier: admin.Identifier);
        var dto = new FakeAgentForCreationDto().Generate();

        // Act
        var add = underTest.SendAsync(new AddAgent.Command(dto));
        await pause.ReachedAsync();
        await rival.SendAsync(new AddAgent.Command(dto));
        pause.Release();

        // Assert
        await ShouldBeConflictAsync(add, "clientId", "An agent with this client ID already exists.");
    }

    private static async Task ShouldBeConflictAsync(Task request, string property, string message)
    {
        var exception = await Should.ThrowAsync<ConflictException>(request);
        exception.Property.ShouldBe(property);
        exception.Message.ShouldBe(message);
    }
}
