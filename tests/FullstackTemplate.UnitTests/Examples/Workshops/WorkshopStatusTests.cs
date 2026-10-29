namespace FullstackTemplate.UnitTests.Examples.Workshops;

using FullstackTemplate.Server.Examples.Workshops;
using FullstackTemplate.Server.Exceptions;
using Shouldly;

public class WorkshopStatusTests
{
    public static IEnumerable<Func<(string From, string To, bool Allowed)>> Moves()
    {
        string[] names = ["Draft", "Scheduled", "Cancelled", "Completed"];
        HashSet<(string, string)> allowed =
        [
            ("Draft", "Scheduled"), ("Draft", "Cancelled"), ("Scheduled", "Cancelled"), ("Scheduled", "Completed"),
        ];
        foreach (var from in names)
        foreach (var to in names)
            yield return () => (from, to, allowed.Contains((from, to)));
    }

    [Test]
    [MethodDataSource(nameof(Moves))]
    public void a_status_moves_only_to_the_statuses_of_its_table(string from, string to, bool allowed)
    {
        // Arrange
        var status = WorkshopStatus.Of(from);
        var target = WorkshopStatus.Of(to);

        // Act
        var canMove = status.CanMoveTo(target);
        var move = () => status.MoveTo(target);

        // Assert
        canMove.ShouldBe(allowed);
        if (allowed)
        {
            move().ShouldBe(target);
        }
        else
        {
            var refusal = move.ShouldThrow<ValidationException>();
            refusal.Property.ShouldBe("status");
            refusal.Message.ShouldBe($"A {from.ToLowerInvariant()} workshop cannot be {to.ToLowerInvariant()}.");
        }
    }

    [Test]
    public void only_a_draft_can_be_edited()
    {
        // Act
        var editable = WorkshopStatus.ListNames().Where(name => WorkshopStatus.Of(name).CanBeEdited);

        // Assert
        editable.ShouldBe(["Draft"]);
    }

    [Test]
    public void the_names_are_in_the_order_of_the_stages()
    {
        // Act
        var names = WorkshopStatus.ListNames();

        // Assert
        names.ShouldBe(["Draft", "Scheduled", "Cancelled", "Completed"]);
    }

    [Test]
    public void a_name_that_is_not_a_status_gives_a_validation_error()
    {
        // Act
        var act = () => WorkshopStatus.Of("Postponed");

        // Assert
        act.ShouldThrow<ValidationException>().Property.ShouldBe("status");
    }
}
