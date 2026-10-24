namespace FullstackTemplate.FunctionalTests.FunctionalTests.AuditEntries;

using System.Net;
using System.Net.Http.Json;
using FullstackTemplate.Server.Domain.AuditEntries.Dtos;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using Microsoft.AspNetCore.Mvc;
using Shouldly;
using TestUtilities;

/// <summary>
/// A reader with read_audit_log gets the history of a user over HTTP. Only a reader that also has
/// read_audit_log_sensitive sees the personal values.
/// </summary>
public class GetAuditEntryPageTests : TestBase
{
    [Test]
    public async Task reader_without_the_sensitive_permission_gets_the_history_with_personal_values_hidden()
    {
        // Arrange
        var (reader, target) = await AddReaderAndTargetAsync(Permission.ReadAuditLog());
        FactoryClient.SignInAsAppUser(reader.Identifier);

        // Act
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.AuditEntries.GetPage(target.Id));

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var created = (await result.Content.ReadFromJsonAsync<AuditEntryPageDto>())!.Items.ShouldHaveSingleItem();
        (created.Kind, created.EntityId).ShouldBe(("Created", target.Id));
        var values = created.Values.ToDictionary(v => v.Name);
        values["FirstName"].ShouldBe(new AuditValueDto { Name = "FirstName", Hidden = true });
        values["Role"].ShouldBe(new AuditValueDto { Name = "Role", New = "User" });
    }

    [Test]
    public async Task reader_with_the_sensitive_permission_gets_each_value()
    {
        // Arrange
        var (reader, target) = await AddReaderAndTargetAsync(Permission.ReadAuditLog(), Permission.ReadAuditLogSensitive());
        FactoryClient.SignInAsAppUser(reader.Identifier);

        // Act
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.AuditEntries.GetPage(target.Id));

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var values = (await result.Content.ReadFromJsonAsync<AuditEntryPageDto>())!.Items.ShouldHaveSingleItem()
            .Values.ToDictionary(v => v.Name);
        values["FirstName"].ShouldBe(new AuditValueDto { Name = "FirstName", New = target.FirstName });
        values["Email"].ShouldBe(new AuditValueDto { Name = "Email", New = target.Email.Value });
    }

    [Test]
    public async Task reader_without_read_audit_log_gets_a_forbidden_problem()
    {
        // Arrange
        var (reader, target) = await AddReaderAndTargetAsync();
        FactoryClient.SignInAsAppUser(reader.Identifier);

        // Act
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.AuditEntries.GetPage(target.Id));

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await result.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail
            .ShouldBe("You need the read_audit_log permission to do this.");
    }

    private static async Task<(User Reader, User Target)> AddReaderAndTargetAsync(params Permission[] permissions)
    {
        TestGraph.Start(out _)
            .AddTenant(out var tenant)
            .AddUsers(out var users, u => u.Role = "User", u => u.Role = "User");
        users[0].AddPermissions(permissions);
        await InsertAsync(tenant);
        foreach (var user in users)
            await InsertAsync(user);
        return (users[0], users[1]);
    }
}
