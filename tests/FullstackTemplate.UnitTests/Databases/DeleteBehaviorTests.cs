namespace FullstackTemplate.UnitTests.Databases;

using FullstackTemplate.Server.Databases;
using Microsoft.EntityFrameworkCore;
using Shouldly;

public class DeleteBehaviorTests
{
    [Test]
    public void each_foreign_key_is_cascade_or_client_no_action()
    {
        // Arrange
        using var context = new AppDbContextFactory().CreateDbContext([]);

        // Act
        var others = context.Model.GetEntityTypes()
            .SelectMany(e => e.GetDeclaredForeignKeys())
            .Where(fk => fk.DeleteBehavior is not (DeleteBehavior.Cascade or DeleteBehavior.ClientNoAction))
            .Select(fk => $"{fk.DeclaringEntityType.ClrType.Name} to {fk.PrincipalEntityType.ClrType.Name}: {fk.DeleteBehavior}")
            .ToList();

        // Assert
        others.ShouldBeEmpty(
            "The save turns a delete into a soft delete, so a delete must not change another aggregate. Use ClientNoAction for a link to another aggregate. Restrict, NoAction, SetNull, and ClientSetNull set the key of a loaded dependent to null.");
    }
}
