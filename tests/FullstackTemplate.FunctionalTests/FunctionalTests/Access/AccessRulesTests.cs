namespace FullstackTemplate.FunctionalTests.FunctionalTests.Access;

using Shouldly;
using TestUtilities.AccessSweep;

public class AccessRulesTests
{
    private static readonly SurfaceCodes Codes = new("403", "404", "409", "401", code => code[0] == '2');
    private static readonly SweepTarget Target = new("GetThing", "GET /things", NamesARecord: false);

    [Test]
    [Arguments(null, "401", false)]
    [Arguments(null, "403", true)]
    [Arguments(null, "200", true)]
    [Arguments(false, "401", false)]
    [Arguments(false, "200", true)]
    [Arguments(false, "404", true)]
    [Arguments(true, "200", false)]
    [Arguments(true, "404", false)]
    [Arguments(true, "401", true)]
    public void a_call_with_no_user_is_a_gap_unless_it_gets_the_code_of_its_entry(bool? anonymous, string code, bool isGap)
    {
        // Arrange
        var openToEachMember = anonymous is { } value
            ? new Dictionary<string, Open> { [Target.Key] = new("A reason.", value) }
            : [];
        var memberCode = anonymous is null ? "403" : "200";
        var outcomes = new Dictionary<SweepCall, Outcome>
        {
            [SweepCall.NoUser] = new(code, ""),
            [SweepCall.MemberWithOwnTenantIds] = new(memberCode, ""),
            [SweepCall.MemberWithUnknownIds] = new(memberCode, ""),
        };

        // Act
        var gaps = AccessRules.Gaps(Codes, Target, outcomes, openToEachMember);

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
