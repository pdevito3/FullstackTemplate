namespace FullstackTemplate.Server.Domain.TimeZones;

using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using Exceptions;

/// <summary>The IANA time zone of a place, for example <c>America/Chicago</c>.</summary>
public sealed partial class TimeZoneId : ValueObject
{
    public const int MaxLength = 100;
    private const string RequestField = "timeZone";

    // tzdata installs these next to the zones. None is the zone of a place, and no browser knows one. The lookup of the
    // host ignores case, so the check ignores case too: "Posixrules" and "Right/America/Chicago" find the same files.
    private static readonly string[] FilesThatAreNotPlaces = ["Factory", "posixrules", "localtime"];
    private static readonly string[] FoldersThatAreNotPlaces = ["right/", "posix/"];

    // A browser can give an old name such as "Asia/Calcutta", which IANA keeps as a link to "Asia/Kolkata". A host can
    // have no file for a link: Ubuntu 26.04 puts them in the package tzdata-legacy. The zone of the link is then found.
    private static readonly FrozenDictionary<string, string> LinkTargets = TimeZoneLinks.All
        .ToFrozenDictionary(link => link.Name, link => link.Target);

    // macOS finds "america/chicago" and keeps the first case that the process asked for as the ID of the zone. This
    // map refuses a wrong case of a listed zone or a link before the lookup. Windows lists its zones by Windows ID.
    private static readonly FrozenDictionary<string, string> IanaIdsIgnoringCase = TimeZoneInfo.GetSystemTimeZones()
        .Where(zone => zone.HasIanaId)
        .Select(zone => zone.Id)
        .Concat(LinkTargets.Keys)
        .DistinctBy(id => id, StringComparer.OrdinalIgnoreCase)
        .ToFrozenDictionary(id => id, id => id, StringComparer.OrdinalIgnoreCase);

    private TimeZoneInfo? _zone;

    public string Value { get; private set; } = null!;

    // EF Core sets only Value when it loads a row, so the zone is found from the value on first use
    private TimeZoneInfo Zone => _zone ??= Find(Value);

    private TimeZoneId(string value)
    {
        _zone = Find(value);
        Value = value;
    }

    public static TimeZoneId Of(string value) => new(value);

    public override string ToString() => Value;

    public (DateOnly Date, TimeOnly Time) ToLocal(DateTimeOffset instant)
    {
        var local = TimeZoneInfo.ConvertTime(instant, Zone).DateTime;
        return (DateOnly.FromDateTime(local), TimeOnly.FromDateTime(local));
    }

    /// <summary>
    /// The instant of a wall clock time at the place. A time in the spring gap does not exist, so it gives null. A time
    /// in the repeated fall hour gives the first of the two instants.
    /// </summary>
    public DateTimeOffset? ToInstant(DateOnly date, TimeOnly time)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);

        // An offset near the time is a candidate. A candidate that converts back to the same wall clock time is an
        // instant of it. IsInvalidTime and GetUtcOffset alone are wrong for some changes, for example the gap at
        // midnight in Asuncion on 2024-10-06.
        var instants = new[] { local.AddDays(-1), local, local.AddDays(1) }
            .Select(near => new DateTimeOffset(local, Zone.GetUtcOffset(near)))
            .Where(instant => TimeZoneInfo.ConvertTime(instant, Zone).DateTime == local)
            .ToList();
        return instants.Count == 0 ? null : instants.Min().ToUniversalTime();
    }

    private static TimeZoneInfo Find(string value)
    {
        ValidationException.ThrowWhenNullOrWhitespace(value, RequestField, "Please provide a time zone.");
        ValidationException.ThrowWhenLongerThan(value, MaxLength, RequestField,
            $"A time zone can have no more than {MaxLength} characters.");

        // A Windows ID such as "Central Standard Time" also resolves, so the check keeps only IANA IDs.
        var wrongCase = IanaIdsIgnoringCase.TryGetValue(value, out var listed) && listed != value;
        TimeZoneInfo? zone = null;
        var found = !wrongCase
            && (TryFindIanaZone(value, out zone)
                || LinkTargets.TryGetValue(value, out var target) && TryFindIanaZone(target, out zone));
        ValidationException.Must(found, RequestField,
            $"'{value}' is not a time zone that the server knows. Use an IANA ID such as America/Chicago.");
        // tzdata gives Factory to a machine whose local time is not known yet. Each name of a place starts with a capital
        // letter. The lists also refuse a capital form of a file, such as Posixrules, because the lookup ignores case.
        ValidationException.Must(char.IsAsciiLetterUpper(value[0])
            && !FilesThatAreNotPlaces.Contains(value, StringComparer.OrdinalIgnoreCase)
            && !FoldersThatAreNotPlaces.Any(folder => value.StartsWith(folder, StringComparison.OrdinalIgnoreCase)),
            RequestField,
            $"'{value}' is not the time zone of a place. Use an IANA ID such as America/Chicago.");

        return zone!;
    }

    private static bool TryFindIanaZone(string id, [NotNullWhen(true)] out TimeZoneInfo? zone) =>
        TimeZoneInfo.TryFindSystemTimeZoneById(id, out zone)
        && zone.HasIanaId
        // The cached zone of an alias keeps the case of the first lookup, so compare with no case
        && zone.Id.Equals(id, StringComparison.OrdinalIgnoreCase);

    private TimeZoneId() { } // EF Core
}
