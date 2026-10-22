namespace FullstackTemplate.IntegrationTests.Databases;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using Shouldly;

/// <summary>
/// Two requests can pass the duplicate check of a handler at the same time. Then the unique index stops the second
/// save, and the DbContext gives a conflict on the request field, not a database failure.
/// </summary>
public class ConstraintConflictTranslationTests : TestBase
{
    [Test]
    public async Task duplicate_save_is_a_conflict_on_the_field_of_the_index()
    {
        // Arrange
        var name = $"Tenant {Guid.NewGuid():N}";
        await new TestingServiceScope().SetupTenantAsync(name);
        var testingServiceScope = new TestingServiceScope();

        // Act
        Func<Task> act = () => testingServiceScope.StartGraph(out _).AddTenant(out _, t => t.Name = name).BuildAsync();

        // Assert
        var exception = await act.ShouldThrowAsync<ConstraintConflictException>();
        exception.Constraint.ShouldBe("ix_tenants_name");
        exception.Property.ShouldBe("name");
        exception.Message.ShouldBe(ConstraintConflictException.DuplicateMessage);
    }
}
