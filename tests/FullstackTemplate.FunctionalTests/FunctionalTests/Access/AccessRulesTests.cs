namespace FullstackTemplate.FunctionalTests.FunctionalTests.Access;

using Shouldly;
using TestUtilities.AccessSweep;

public class AccessRulesTests
{
    private static readonly SurfaceCodes Codes = new("403", "404", "409", "401", code => code[0] == '2');
    private static readonly SweepTarget Target = new("GetThing", "GET /things", NamesARecord: false);

    [Test]
    [Arguments(null, "401", false, false)]
    [Arguments(null, "403", false, true)]
    [Arguments(null, "200", false, true)]
    [Arguments(false, "401", false, false)]
    [Arguments(false, "200", false, true)]
    [Arguments(false, "404", false, true)]
    [Arguments(true, "200", false, false)]
    [Arguments(true, "404", true, false)]
    [Arguments(true, "404", false, true)]
    [Arguments(true, "401", false, true)]
    public void a_call_with_no_user_is_a_gap_unless_it_gets_the_code_of_its_entry(
        bool? anonymous, string code, bool namesARecord, bool isGap)
    {
        // Arrange
        var target = Target with { NamesARecord = namesARecord };
        var openToEachMember = anonymous is { } value
            ? new Dictionary<string, Open> { [target.Key] = new("A reason.", value) }
            : [];
        var memberCode = anonymous is null ? "403" : "200";
        var outcomes = new Dictionary<SweepCall, Outcome>
        {
            [SweepCall.NoUser] = new(code, ""),
            [SweepCall.MemberWithOwnTenantIds] = new(memberCode, ""),
            [SweepCall.MemberWithUnknownIds] = new(memberCode, ""),
        };

        // Act
        var gaps = AccessRules.Gaps(Codes, target, outcomes, openToEachMember);

        // Assert
        (gaps.Count > 0).ShouldBe(isGap, string.Join("; ", gaps));
    }

    [Test]
    public void a_surface_with_no_unauthenticated_code_gets_no_call_with_no_user()
    {
        // Arrange
        var codes = Codes with { Unauthenticated = null };

        // Act
        var calls = AccessRules.CallsFor(codes, Target);

        // Assert
        calls.ShouldNotContain(SweepCall.NoUser);
        AccessRules.CallsFor(Codes, Target).ShouldContain(SweepCall.NoUser);
    }
}
