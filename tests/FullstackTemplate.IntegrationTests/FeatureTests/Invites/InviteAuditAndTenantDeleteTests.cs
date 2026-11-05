namespace FullstackTemplate.IntegrationTests.FeatureTests.Invites;

using FullstackTemplate.Server.Domain.AuditEntries.Dtos;
using FullstackTemplate.Server.Domain.AuditEntries.Features;
using FullstackTemplate.Server.Domain.Invites;
using FullstackTemplate.Server.Domain.Invites.Dtos;
using FullstackTemplate.Server.Domain.Invites.Features;
using FullstackTemplate.Server.Domain.Tenants.Features;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.Server.Resources;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Fakes.Invite;
using Microsoft.EntityFrameworkCore;
using Shouldly;

public class InviteAuditAndTenantDeleteTests : TestBase
{
    [Test, Property("Rule", "INV-12")]
    public async Task the_audit_log_hides_the_token_hash_from_a_reader_without_the_sensitive_permission()
    {
        // Arrange
        var scope = new TestingServiceScope();
        var graph = scope.StartGraph(out _).AddUser(out var reader, u => u.Role = UserRole.User().Value);
        reader.AddPermissions([Permission.ReadAuditLog()]);
        await graph.BuildAsync();
        var added = await new TestingServiceScope().SendAsync(new AddInvite.Command(
            new FakeInviteForCreationDto().Generate() with { Role = UserRole.User().Value, Email = "ada@example.com" }));
        var readerScope = new TestingServiceScope();
        readerScope.SetUserWithDefaults(identifier: reader.Identifier);

        // Act
        var page = await readerScope.SendAsync(new GetAuditEntryPage.Query(
            AuditEntryParametersDto.From(added.Invite.Id, null, null)));

        // Assert
        var values = page.Items.ShouldHaveSingleItem().Values.ToDictionary(v => v.Name);
        values[nameof(Invite.TokenHash)].ShouldBe(new AuditValueDto { Name = nameof(Invite.TokenHash), Hidden = true });
        values[nameof(Invite.Email)].Hidden.ShouldBeTrue();
        values[nameof(Invite.Role)].ShouldBe(new AuditValueDto { Name = nameof(Invite.Role), New = UserRole.User().Value });
    }

    [Test, Property("Rule", "INV-11")]
    public async Task a_tenant_delete_deletes_each_invite_of_the_tenant_and_the_link_stops_working()
    {
        // Arrange
        var token = InviteToken.New().Value;
        var scope = new TestingServiceScope();
        await scope.StartGraph(out _)
            .AddTenant(out var tenant)
            .AddUser(out var admin, u => u.Role = UserRole.Admin().Value)
            .Under(tenant).AddInvites(out var invites, i => i.Token = token, _ => { })
            .BuildAsync();
        scope.SetUserWithDefaults(identifier: admin.Identifier);

        // Act
        await scope.SendAsync(new DeleteTenant.Command(tenant.Id));

        // Assert
        var ids = invites.Select(i => i.Id).ToList();
        (await new TestingServiceScope().ExecuteDbContextAsync(db => db.Invites
            .IgnoreQueryFilters([QueryFilterNames.Tenant, QueryFilterNames.SoftDelete])
            .Where(i => ids.Contains(i.Id)).Select(i => i.IsDeleted).ToListAsync())).ShouldBe([true, true]);
        var colleague = new TestingServiceScope();
        colleague.SignInAsNewUser();
        var preview = () => colleague.SendAsync(new GetInvitePreview.Query(new InviteTokenDto { Token = token }));
        await preview.ShouldThrowAsync<NotFoundException>();
    }
}
