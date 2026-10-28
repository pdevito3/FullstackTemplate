namespace FullstackTemplate.UnitTests.Docs;

using System.Diagnostics;
using FullstackTemplate.Server.Exceptions;
using Shouldly;

// The example of docs/backend/features-and-cqrs.md, "One rule set for two callers". A test below checks that the doc shows
// this code.

public sealed record Room(string Id, int DaysAhead);

public sealed class Booking
{
    private Booking(string roomId, DateOnly day) => (RoomId, Day) = (roomId, day);

    public string RoomId { get; }
    public DateOnly Day { get; }

    public static Booking Create(BookableDay day) => new(day.RoomId, day.Day);
}

public sealed record BookingCase
{
    public BookingCase(Room room, IReadOnlyCollection<Booking> bookings, DateOnly today)
    {
        if (bookings.Any(b => b.RoomId != room.Id))
            throw new ArgumentException("Give only the bookings of the room.", nameof(bookings));
        (Room, Bookings, Today) = (room, bookings, today);
    }

    public Room Room { get; }
    public IReadOnlyCollection<Booking> Bookings { get; }
    public DateOnly Today { get; }
}

public abstract record Refusal
{
    private Refusal(string field, string message) => (Field, Message) = (field, message);

    public string Field { get; }
    public string Message { get; }

    public sealed record NotOffered(string Field, string Message) : Refusal(Field, Message);
    public sealed record Taken(string Field, string Message) : Refusal(Field, Message);
}

public sealed class BookableDay
{
    internal BookableDay(string roomId, DateOnly day) => (RoomId, Day) = (roomId, day);

    public string RoomId { get; }
    public DateOnly Day { get; }
}

public abstract record Decision
{
    private Decision() { }

    public sealed record Offered(BookableDay Day) : Decision;
    public sealed record Refused(Refusal Refusal) : Decision;
}

public static class BookingRules
{
    private static readonly Func<BookingCase, DateOnly, Refusal?>[] Rules = [InWindow, Free];

    public static Decision Decide(BookingCase booking, DateOnly day)
        => Rules.Select(rule => rule(booking, day)).FirstOrDefault(r => r is not null) is { } refusal
            ? new Decision.Refused(refusal)
            : new Decision.Offered(new BookableDay(booking.Room.Id, day));

    private static Refusal.NotOffered? InWindow(BookingCase booking, DateOnly day)
        => day < booking.Today || day > booking.Today.AddDays(booking.Room.DaysAhead)
            ? new Refusal.NotOffered("day", $"Pick a day from today to {booking.Room.DaysAhead} days ahead.")
            : null;

    private static Refusal.Taken? Free(BookingCase booking, DateOnly day)
        => booking.Bookings.Any(b => b.Day == day)
            ? new Refusal.Taken("day", "Someone booked this day. Pick another day.")
            : null;
}

public static class BookingHandlers
{
    public static IReadOnlyList<DateOnly> FreeDays(BookingCase booking, IEnumerable<DateOnly> candidates)
        => candidates.Where(day => BookingRules.Decide(booking, day) is Decision.Offered).ToList();

    public static Booking Book(BookingCase booking, DateOnly day) => BookingRules.Decide(booking, day) switch
    {
        Decision.Offered offered => Booking.Create(offered.Day),
        Decision.Refused { Refusal: Refusal.Taken taken } => throw new ConflictException(taken.Field, taken.Message),
        Decision.Refused { Refusal: Refusal.NotOffered notOffered }
            => throw new ValidationException(notOffered.Field, notOffered.Message),
        _ => throw new UnreachableException(),
    };
}

/// <summary>
/// The three tests that each use of the decision shape needs: one for each rule, one for the order of the rules, and
/// one that ties the list and the command together.
/// </summary>
public class DecisionShapeExampleTests
{
    private static readonly DateOnly Today = new(2026, 3, 2);
    private static readonly Room Room = new("roo_1", DaysAhead: 7);

    public static IEnumerable<Func<(DateOnly Day, DateOnly[] Taken, Type Kind, string Field, string Message)>> OneBrokenRule()
    {
        yield return () => (Today.AddDays(-1), [], typeof(Refusal.NotOffered), "day",
            "Pick a day from today to 7 days ahead.");
        yield return () => (Today.AddDays(8), [], typeof(Refusal.NotOffered), "day", "Pick a day from today to 7 days ahead.");
        yield return () => (Today.AddDays(3), [Today.AddDays(3)], typeof(Refusal.Taken), "day",
            "Someone booked this day. Pick another day.");
    }

    [Test]
    [MethodDataSource(nameof(OneBrokenRule))]
    public void a_case_that_breaks_one_rule_gets_the_refusal_of_that_rule(
        DateOnly day, DateOnly[] taken, Type kind, string field, string message)
    {
        // Arrange
        var booking = Case(taken);

        // Act
        var decision = BookingRules.Decide(booking, day);

        // Assert
        var refusal = decision.ShouldBeOfType<Decision.Refused>().Refusal;
        refusal.ShouldBeOfType(kind);
        refusal.Field.ShouldBe(field);
        refusal.Message.ShouldBe(message);
    }

    [Test]
    public void a_case_that_breaks_each_rule_gets_the_refusal_of_the_first_rule_that_it_breaks()
    {
        // Arrange
        var outside = Today.AddDays(8);
        var inside = Today.AddDays(3);

        // Act
        var breaksEach = BookingRules.Decide(Case([outside]), outside);
        var breaksOnlyFree = BookingRules.Decide(Case([inside]), inside);
        var breaksNone = BookingRules.Decide(Case([]), inside);

        // Assert
        breaksEach.ShouldBeOfType<Decision.Refused>().Refusal.ShouldBeOfType<Refusal.NotOffered>();
        breaksOnlyFree.ShouldBeOfType<Decision.Refused>().Refusal.ShouldBeOfType<Refusal.Taken>();
        breaksNone.ShouldBeOfType<Decision.Offered>().Day.Day.ShouldBe(inside);
    }

    [Test]
    public void the_command_books_each_day_that_the_list_offers_and_refuses_each_other_day()
    {
        // Arrange
        var booking = Case([Today.AddDays(2), Today.AddDays(5)]);
        var candidates = Enumerable.Range(-2, 12).Select(Today.AddDays).ToList();

        // Act
        var free = BookingHandlers.FreeDays(booking, candidates);

        // Assert
        free.ShouldNotBeEmpty();
        free.ShouldAllBe(day => BookingHandlers.Book(booking, day).Day == day);
        candidates.Except(free).ShouldAllBe(day => Refuses(booking, day));
    }

    [Test]
    public void the_command_maps_a_taken_day_to_a_conflict_and_a_day_outside_the_window_to_a_validation_error()
    {
        // Arrange
        var taken = Today.AddDays(3);
        var booking = Case([taken]);

        // Act
        var conflict = Should.Throw<ConflictException>(() => BookingHandlers.Book(booking, taken));
        var invalid = Should.Throw<ValidationException>(() => BookingHandlers.Book(booking, Today.AddDays(8)));

        // Assert
        conflict.Property.ShouldBe("day");
        invalid.Property.ShouldBe("day");
    }

    [Test]
    [Arguments(typeof(Refusal))]
    [Arguments(typeof(Decision))]
    public void each_kind_of_a_closed_type_is_nested_in_it(Type closed)
    {
        // Act
        var kinds = typeof(DecisionShapeExampleTests).Assembly.GetTypes().Where(t => t.IsSubclassOf(closed));

        // Assert
        kinds.ShouldAllBe(kind => kind.DeclaringType == closed);
    }

    [Test]
    public void a_case_refuses_a_booking_of_another_room()
    {
        // Arrange
        var otherRoom = Room with { Id = "roo_2" };
        var bookingOfOtherRoom = Booked(otherRoom, Today);

        // Act
        var act = () => new BookingCase(Room, [bookingOfOtherRoom], Today);

        // Assert
        act.ShouldThrow<ArgumentException>().ParamName.ShouldBe("bookings");
    }

    [Test]
    public void the_doc_shows_the_code_of_this_example()
    {
        // Arrange
        var root = FindRepositoryRoot();
        var doc = File.ReadAllText(Path.Combine(root, "docs", "backend", "features-and-cqrs.md")).ReplaceLineEndings("\n");
        var source = File.ReadAllText(Path.Combine(root, "tests", "FullstackTemplate.UnitTests", "Docs",
            "DecisionShapeExampleTests.cs")).ReplaceLineEndings("\n");

        // Act
        var section = doc[doc.IndexOf("\n## One rule set for two callers\n", StringComparison.Ordinal)..];
        var start = section.IndexOf("```csharp\n", StringComparison.Ordinal) + "```csharp\n".Length;
        var sample = section[start..section.IndexOf("\n```", start, StringComparison.Ordinal)];

        // Assert
        sample.ShouldStartWith("public sealed record Room");
        source.ShouldContain(sample);
    }

    private static bool Refuses(BookingCase booking, DateOnly day)
    {
        try
        {
            BookingHandlers.Book(booking, day);
            return false;
        }
        catch (Exception e) when (e is ValidationException or ConflictException)
        {
            return true;
        }
    }

    private static BookingCase Case(DateOnly[] taken)
        => new(Room, taken.Select(day => Booked(Room, day)).ToList(), Today);

    // The window starts on the day, so a test can arrange a booking on any day
    private static Booking Booked(Room room, DateOnly day)
        => BookingHandlers.Book(new BookingCase(room with { DaysAhead = 365 }, [], day), day);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FullstackTemplate.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
    }
}
