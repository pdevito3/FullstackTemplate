namespace FullstackTemplate.UnitTests.Resources.Jobs;

using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Resources.Jobs;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

public class JobOperatorsTests
{
    private const string Operator = "154c705b-f83d-4657-a9dd-af6ad1d36796";

    public static IEnumerable<Func<CallerIdentity>> CallersThatAreNotAUser() =>
    [
        () => new CallerIdentity.Anonymous(),
        () => new CallerIdentity.System(),
        () => new CallerIdentity.TenantJob("ten_1"),
        () => new CallerIdentity.AllTenantsClient("reporting", CanManage: true),
        // The token of an agent that acts for an operator does not open the dashboard
        () => new CallerIdentity.DelegatedAgent("agt_1", "agent-client", Operator, new LiveUser("usr_1", "ten_1")),
        () => new CallerIdentity.MachineAgent("agt_1", "machine-client", "ten_1"),
        () => new CallerIdentity.UnknownClient("other-client"),
    ];

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task a_user_on_the_list_is_an_operator_with_or_without_a_live_user(bool hasLiveUser)
    {
        // Arrange
        var operators = OperatorsFor(new CallerIdentity.User(Operator, hasLiveUser ? new LiveUser("usr_1", "ten_1") : null), [Operator]);

        // Act & Assert
        (await operators.IncludesCallerAsync(CancellationToken.None)).ShouldBeTrue();
    }

    [Test]
    [Arguments("someone-else")]
    [Arguments("154C705B-F83D-4657-A9DD-AF6AD1D36796")]
    public async Task a_user_whose_subject_is_not_on_the_list_is_not_an_operator(string subject)
    {
        // Arrange
        var operators = OperatorsFor(new CallerIdentity.User(subject, null), [Operator]);

        // Act & Assert
        (await operators.IncludesCallerAsync(CancellationToken.None)).ShouldBeFalse();
    }

    [Test]
    public async Task an_empty_list_lets_no_user_in()
    {
        // Arrange
        var operators = OperatorsFor(new CallerIdentity.User(Operator, null), []);

        // Act & Assert
        (await operators.IncludesCallerAsync(CancellationToken.None)).ShouldBeFalse();
    }

    [Test]
    [MethodDataSource(nameof(CallersThatAreNotAUser))]
    public async Task a_caller_that_is_not_a_user_is_not_an_operator(CallerIdentity caller)
    {
        // Arrange
        var operators = OperatorsFor(caller, [Operator]);

        // Act & Assert
        (await operators.IncludesCallerAsync(CancellationToken.None)).ShouldBeFalse();
    }

    private static JobOperators OperatorsFor(CallerIdentity caller, string[] operatorSubjects)
    {
        var callers = Substitute.For<ICallerIdentityResolver>();
        callers.GetAsync(Arg.Any<CancellationToken>()).Returns(caller);
        return new JobOperators(callers, Options.Create(new BackgroundJobsOptions { OperatorSubjects = operatorSubjects }));
    }
}
