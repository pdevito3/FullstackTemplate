namespace FullstackTemplate.UnitTests.Domain.Users;

using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.SharedTestHelpers.Fakes.User;
using Shouldly;

public class TenantAdminsTests
{
    public static IEnumerable<Func<(string Name, AdminChange Change, bool IsAdmin, int OtherAdmins, bool Refused)>> Cases()
    {
        yield return () => ("the only admin becomes a user", new AdminChange.NewRole(UserRole.User()), true, 0, true);
        yield return () => ("the only admin is removed", new AdminChange.Removal(), true, 0, true);
        yield return () => ("the only admin stays an admin", new AdminChange.NewRole(UserRole.Admin()), true, 0, false);
        yield return () => ("one of two admins becomes a user", new AdminChange.NewRole(UserRole.User()), true, 1, false);
        yield return () => ("one of two admins is removed", new AdminChange.Removal(), true, 1, false);
        yield return () => ("a user is removed from a tenant with no admin", new AdminChange.Removal(), false, 0, false);
        yield return () => ("a user becomes an admin", new AdminChange.NewRole(UserRole.Admin()), false, 0, false);
    }

    [Test]
    [Property("Rule", "UM-4")]
    [MethodDataSource(nameof(Cases))]
    public void a_change_that_leaves_the_tenant_with_no_admin_is_refused(
        (string Name, AdminChange Change, bool IsAdmin, int OtherAdmins, bool Refused) row)
    {
        // Arrange
        var user = User.Create(new FakeUserForCreation().Generate() with
        {
            Role = row.IsAdmin ? UserRole.Admin().Value : UserRole.User().Value,
        });
        var adminIds = (row.IsAdmin ? [user.Id] : new List<string>())
            .Concat(Enumerable.Range(1, row.OtherAdmins).Select(n => $"usr_other{n}"))
            .ToList();

        // Act
        var decision = TenantAdmins.Decide(new AdminCase(user, row.Change, adminIds));

        // Assert
        if (row.Refused)
        {
            var refusal = decision.ShouldBeOfType<AdminDecision.Refused>().Refusal.ShouldBeOfType<AdminRefusal.LastAdmin>();
            refusal.Field.ShouldBe("role");
            refusal.Message.ShouldBe("This user is the only admin of the tenant. Make another user an admin first.");
        }
        else
        {
            var proof = decision.ShouldBeOfType<AdminDecision.Kept>().Proof;
            proof.UserId.ShouldBe(user.Id);
            proof.Change.ShouldBe(row.Change);
        }
    }

    [Test]
    public void a_case_refuses_admins_that_do_not_match_the_role_of_the_user()
    {
        // Arrange
        var admin = User.Create(new FakeUserForCreation().Generate() with { Role = UserRole.Admin().Value });
        var member = User.Create(new FakeUserForCreation().Generate() with { Role = UserRole.User().Value });

        // Act
        var adminNotListed = () => new AdminCase(admin, new AdminChange.Removal(), ["usr_other"]);
        var memberListed = () => new AdminCase(member, new AdminChange.Removal(), [member.Id]);

        // Assert
        adminNotListed.ShouldThrow<ArgumentException>();
        memberListed.ShouldThrow<ArgumentException>();
    }

    [Test]
    [Arguments(typeof(AdminChange))]
    [Arguments(typeof(AdminRefusal))]
    [Arguments(typeof(AdminDecision))]
    public void each_kind_of_a_closed_type_is_nested_in_it(Type closed)
    {
        // Act
        var kinds = typeof(TenantAdmins).Assembly.GetTypes().Where(t => t.IsSubclassOf(closed));

        // Assert
        kinds.ShouldAllBe(kind => kind.DeclaringType == closed);
    }
}
