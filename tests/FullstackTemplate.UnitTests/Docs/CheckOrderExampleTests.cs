namespace FullstackTemplate.UnitTests.Docs;

using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Exceptions;
using Shouldly;

public sealed record VisitForReschedule(DateOnly Day, DateOnly Today, uint Version);

public sealed record VisitRescheduled(string VisitId) : DomainEvent;

public sealed class Visit : BaseEntity
{
    public const string IdPrefix = "vis";
    public const string CancelledMessage = "This visit is cancelled. It cannot move.";

    private Visit(DateOnly day) : base(IdPrefix) => Day = day;

    public DateOnly Day { get; private set; }
    public bool IsCancelled { get; private set; }

    public static Visit Create(DateOnly day) => new(day);

    public Visit Cancel()
    {
        IsCancelled = true;
        return this;
    }

    public Visit Reschedule(VisitForReschedule forReschedule)
    {
        GuardVersion(forReschedule.Version);
        GuardIfCancelled();
        if (forReschedule.Day == Day)
            return this;

        ValidationException.Must(forReschedule.Day >= forReschedule.Today, "day", "Pick today or a later day.");
        Day = forReschedule.Day;
        QueueDomainEvent(new VisitRescheduled(Id));

        return this;
    }

    private void GuardIfCancelled()
    {
        if (IsCancelled)
            throw new ValidationException("day", CancelledMessage);
    }
}

public class CheckOrderExampleTests
{
    private static readonly DateOnly Day = new(2026, 3, 2);
    private const uint CurrentVersion = 0;
    private const uint StaleVersion = 1;

    [Test]
    public void a_stale_request_to_a_cancelled_visit_is_a_stale_version()
    {
        // Arrange
        var visit = Visit.Create(Day).Cancel();

        // Act
        var act = () => visit.Reschedule(new VisitForReschedule(Day.AddDays(1), Day, StaleVersion));

        // Assert
        act.ShouldThrow<StaleVersionException>();
    }

    [Test]
    public void a_same_day_request_to_a_cancelled_visit_is_the_validation_error_of_the_state()
    {
        // Arrange
        var visit = Visit.Create(Day).Cancel();

        // Act
        var act = () => visit.Reschedule(new VisitForReschedule(Day, Day, CurrentVersion));

        // Assert
        var error = act.ShouldThrow<ValidationException>();
        error.Property.ShouldBe("day");
        error.Message.ShouldBe(Visit.CancelledMessage);
    }

    [Test]
    public void a_same_day_request_succeeds_with_no_event_after_the_day_has_passed()
    {
        // Arrange
        var visit = Visit.Create(Day);

        // Act
        visit.Reschedule(new VisitForReschedule(Day, Day.AddDays(3), CurrentVersion));

        // Assert
        visit.Day.ShouldBe(Day);
        visit.DomainEvents.ShouldBeEmpty();
    }

    [Test]
    public void a_request_for_a_past_day_is_a_validation_error_on_the_day()
    {
        // Arrange
        var visit = Visit.Create(Day.AddDays(5));

        // Act
        var act = () => visit.Reschedule(new VisitForReschedule(Day.AddDays(1), Day.AddDays(3), CurrentVersion));

        // Assert
        act.ShouldThrow<ValidationException>().Property.ShouldBe("day");
        visit.Day.ShouldBe(Day.AddDays(5));
    }

    [Test]
    public void a_request_for_a_new_day_moves_the_visit_and_queues_the_event()
    {
        // Arrange
        var visit = Visit.Create(Day);

        // Act
        visit.Reschedule(new VisitForReschedule(Day.AddDays(1), Day, CurrentVersion));

        // Assert
        visit.Day.ShouldBe(Day.AddDays(1));
        visit.DomainEvents.ShouldHaveSingleItem().ShouldBe(new VisitRescheduled(visit.Id));
    }

    [Test]
    public void the_doc_shows_the_reschedule_method_of_this_file()
    {
        // Arrange
        var root = FindRepositoryRoot();
        var doc = File.ReadAllText(Path.Combine(root, "docs", "backend", "features-and-cqrs.md")).ReplaceLineEndings("\n");
        var source = File.ReadAllText(Path.Combine(root, "tests", "FullstackTemplate.UnitTests", "Docs",
            "CheckOrderExampleTests.cs")).ReplaceLineEndings("\n");

        // Act
        var section = doc[doc.IndexOf("\n## The order of the checks\n", StringComparison.Ordinal)..];
        var start = section.IndexOf("```csharp\n", StringComparison.Ordinal) + "```csharp\n".Length;
        var sample = section[start..section.IndexOf("\n```", start, StringComparison.Ordinal)];
        var sampleInClass = string.Join('\n', sample.Split('\n').Select(line => line.Length == 0 ? line : "    " + line));

        // Assert
        sample.ShouldStartWith("public Visit Reschedule");
        source.ShouldContain(sampleInClass);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FullstackTemplate.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not find FullstackTemplate.slnx above the test output directory.");
    }
}
