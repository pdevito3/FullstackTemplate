namespace FullstackTemplate.IntegrationTests.Examples.Workshops;

using FullstackTemplate.IntegrationTests.ObjectDeletes;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Examples.Workshops.Features;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Examples.EntityGraphs;
using Shouldly;

/// <summary>The sweep of the bucket reads each storage key column, here the one of the workshop attachments.</summary>
public class WorkshopAttachmentSweepTests : TestBase
{
    [Test]
    public async Task the_object_of_a_live_attachment_of_any_tenant_stays_while_an_orphan_of_its_workshop_goes()
    {
        // Arrange
        await using var bucket = await SweepBucket.CreateAsync();
        await new TestingServiceScope().StartGraph(out _)
            .AddTenant(out var tenant)
            .Under(tenant).AddVenue(out _).AddWorkshop(out var workshop).AddWorkshopAttachment(out var attachment)
            .BuildAsync();
        var named = await bucket.PutAsync(attachment.File.StorageKey);
        var orphan = await bucket.PutAsync($"tenants/{tenant.Id}/workshops/{workshop.Id}/{Guid.NewGuid():N}");

        // Act
        await bucket.SweepAsync(Latest(named.LastModified, orphan.LastModified) + SweepBucket.Grace + TimeSpan.FromSeconds(1));

        // Assert
        (await bucket.KeysAsync()).ShouldBe([named.Key]);
    }

    [Test]
    public async Task the_object_of_a_deleted_attachment_goes_when_its_delete_job_did_not_run()
    {
        // Arrange
        await using var bucket = await SweepBucket.CreateAsync();
        await new TestingServiceScope().StartGraph(out _)
            .AddTenant(out var tenant)
            .AddUser(out var admin, u => u.Role = UserRole.Admin().Value)
            .Under(tenant).AddVenue(out _).AddWorkshop(out var workshop).AddWorkshopAttachment(out var attachment)
            .BuildAsync();
        var stored = await bucket.PutAsync(attachment.File.StorageKey);
        var deleter = new TestingServiceScope();
        deleter.SetUserWithDefaults(identifier: admin.Identifier);
        await deleter.SendAsync(new DeleteWorkshopAttachment.Command(workshop.Id, attachment.Id));

        // Act
        await bucket.SweepAsync(stored.LastModified + SweepBucket.Grace + TimeSpan.FromSeconds(1));

        // Assert
        (await bucket.KeysAsync()).ShouldBeEmpty();
    }

    private static DateTimeOffset Latest(DateTimeOffset first, DateTimeOffset second) => first > second ? first : second;
}
