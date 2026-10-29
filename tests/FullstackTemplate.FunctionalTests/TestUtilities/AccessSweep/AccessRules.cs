namespace FullstackTemplate.FunctionalTests.TestUtilities.AccessSweep;

public enum SweepCall
{
    NoUser,
    MemberWithOwnTenantIds,
    MemberWithUnknownIds,
    AdminWithOtherTenantIds,
    AdminWithUnknownIds,
    AdminWithOwnTenantIds,
}

/// <param name="Key">The endpoint name, the tool name, or the full gRPC method name. The allow list uses it.</param>
public sealed record SweepTarget(string Key, string Label, bool NamesARecord);

/// <param name="Anonymous">A call with no user does not get the code of a missing token.</param>
public sealed record Open(string Reason, bool Anonymous = false);

public sealed record Outcome(string Code, string Detail);

/// <param name="StaleVersion">The code of a stale version. The handler must find the record to compare its version.</param>
/// <param name="Unauthenticated">
/// The code of a call with no token. It is null for a surface that refuses such a call before it reaches an entrypoint,
/// so the sweep makes no call with no user there.
/// </param>
public sealed record SurfaceCodes(
    string Forbidden, string NotFound, string StaleVersion, string? Unauthenticated, Func<string, bool> IsSuccess);

public sealed class SweepSetupException(string message) : Exception(message);

public static class AccessRules
{
    private static readonly SweepCall[] NoUserCalls = [SweepCall.NoUser];
    private static readonly SweepCall[] MemberCalls = [SweepCall.MemberWithOwnTenantIds, SweepCall.MemberWithUnknownIds];
    // The call with the IDs of the own tenant is the control of the two 404 calls, so that an ID of the wrong kind
    // cannot pass. It runs last.
    private static readonly SweepCall[] AdminCalls =
        [SweepCall.AdminWithOtherTenantIds, SweepCall.AdminWithUnknownIds, SweepCall.AdminWithOwnTenantIds];

    // With no record in the request, the admin calls are the same request, and no other tenant can show
    public static IReadOnlyList<SweepCall> CallsFor(SurfaceCodes codes, SweepTarget target)
        => [.. codes.Unauthenticated is null ? [] : NoUserCalls, .. MemberCalls, .. target.NamesARecord ? AdminCalls : []];

    public static IReadOnlyList<string> Gaps(
        SurfaceCodes codes,
        SweepTarget target,
        IReadOnlyDictionary<SweepCall, Outcome> outcomes,
        IReadOnlyDictionary<string, Open> openToEachMember)
    {
        var entry = openToEachMember.GetValueOrDefault(target.Key);
        var gaps = new List<string>();

        foreach (var (call, outcome) in outcomes)
        {
            var (accepts, expected) = ExpectationOf(codes, call, entry, target);
            if (!accepts(outcome.Code))
                gaps.Add(GapLine(target, call, expected, outcome));
        }

        var memberOutcomes = MemberCalls.Where(outcomes.ContainsKey).Select(call => outcomes[call].Code).ToList();
        if (entry is not null && memberOutcomes.Count > 0 && memberOutcomes.All(code => code == codes.Forbidden))
            gaps.Add($"{NameOf(target)}: it is on the allow list, but a member with no permissions gets "
                + $"{codes.Forbidden}. Remove the entry from the allow list.");

        return gaps;
    }

    private static (Func<string, bool> Accepts, string Expected) ExpectationOf(
        SurfaceCodes codes, SweepCall call, Open? entry, SweepTarget target)
        => call switch
        {
            SweepCall.NoUser when entry is { Anonymous: true } && target.NamesARecord
                => (code => codes.IsSuccess(code) || code == codes.NotFound,
                    $"a success or {codes.NotFound}, because its allow-list entry is anonymous"),
            SweepCall.NoUser when entry is { Anonymous: true }
                => (codes.IsSuccess, "a success, because its allow-list entry is anonymous"),
            SweepCall.NoUser
                => (code => code == codes.Unauthenticated,
                    $"{codes.Unauthenticated}, because no allow-list entry marks it anonymous"),
            SweepCall.MemberWithOwnTenantIds when entry is not null
                => (codes.IsSuccess, "a success, because it is on the allow list"),
            SweepCall.MemberWithUnknownIds when entry is not null
                => (code => codes.IsSuccess(code) || code == codes.NotFound,
                    $"a success or {codes.NotFound}, because it is on the allow list"),
            SweepCall.MemberWithOwnTenantIds or SweepCall.MemberWithUnknownIds
                => (code => code == codes.Forbidden, codes.Forbidden),
            SweepCall.AdminWithOtherTenantIds or SweepCall.AdminWithUnknownIds
                => (code => code == codes.NotFound, codes.NotFound),
            SweepCall.AdminWithOwnTenantIds
                => (code => codes.IsSuccess(code) || code == codes.StaleVersion,
                    $"a success or {codes.StaleVersion}, because the record exists (a {codes.NotFound} here means that "
                        + $"the ID has the wrong kind, so the {codes.NotFound} of the other admin calls proves nothing)"),
            _ => throw new ArgumentOutOfRangeException(nameof(call), call, null),
        };

    public static IReadOnlyList<string> DuplicateKeyGaps(IEnumerable<SweepTarget> targets)
        => targets.GroupBy(target => target.Key)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key}: {group.Count()} entrypoints have this name "
                + $"({string.Join(", ", group.Select(target => target.Label))}). Give each one its own name.")
            .ToList();

    public static string Describe(SweepCall call) => call switch
    {
        SweepCall.NoUser => "a call with no user, with unknown IDs",
        SweepCall.MemberWithOwnTenantIds => "a member with no permissions, with the IDs of records of the own tenant",
        SweepCall.MemberWithUnknownIds => "a member with no permissions, with unknown IDs",
        SweepCall.AdminWithOtherTenantIds => "an admin with all permissions, with the IDs of records of another tenant",
        SweepCall.AdminWithUnknownIds => "an admin with all permissions, with unknown IDs",
        SweepCall.AdminWithOwnTenantIds => "an admin with all permissions, with the IDs of records of the own tenant",
        _ => throw new ArgumentOutOfRangeException(nameof(call), call, null),
    };

    public static string NameOf(SweepTarget target)
        => target.Label.Contains(target.Key, StringComparison.Ordinal) ? target.Label : $"{target.Label} [{target.Key}]";

    private static string GapLine(SweepTarget target, SweepCall call, string expected, Outcome actual)
        => $"{NameOf(target)}: {Describe(call)}: expected {expected}, got {actual.Code}"
            + (actual.Detail.Length > 0 ? $" ({actual.Detail})" : "");
}
