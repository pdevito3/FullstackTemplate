namespace FullstackTemplate.UnitTests.Domain.Users;

using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.SharedTestHelpers.Fakes.User;
using Shouldly;

public class TenantManagersTests
{
    private static readonly ManagerChange ToUser = new ManagerChange.NewRoles(new HashSet<UserRole> { UserRole.User() });
    private static readonly ManagerChange ToAdmin = new ManagerChange.NewRoles(new HashSet<UserRole> { UserRole.Admin() });
    private static readonly ManagerChange ToAdminAndUser =
        new ManagerChange.NewRoles(new HashSet<UserRole> { UserRole.Admin(), UserRole.User() });
    private static readonly ManagerChange ToNoRole = new ManagerChange.NewRoles(new HashSet<UserRole>());
    private static readonly ManagerChange Removal = new ManagerChange.Removal();
    private static readonly ManagerChange LosesAuditLog = new ManagerChange.PermissionRemoval(Permission.ReadAuditLog());

    public static IEnumerable<Func<(string Name, User User, ManagerChange Change, int OtherManagers, string? RefusedField)>> Cases()
    {
        yield return () => ("the only manager becomes a user", Admin(), ToUser, 0, "roles");
        yield return () => ("the only manager loses every role", Admin(), ToNoRole, 0, "roles");
        yield return () => ("the only manager keeps the admin role and gets the user role", Admin(), ToAdminAndUser, 0, null);
        yield return () => ("the only manager is removed", Admin(), Removal, 0, "role");
        yield return () => ("the only manager loses a permission", Admin(), LosesAuditLog, 0, "permission");
        yield return () => ("the only manager stays an admin", Admin(), ToAdmin, 0, null);
        yield return () => ("one of two managers becomes a user", Admin(), ToUser, 1, null);
        yield return () => ("one of two managers is removed", Admin(), Removal, 1, null);
        yield return () => ("one of two managers loses a permission", Admin(), LosesAuditLog, 1, null);
        yield return () => ("a user is removed from a tenant with no manager", Member(), Removal, 0, null);
        yield return () => ("a user becomes an admin", Member(), ToAdmin, 0, null);
        yield return () => ("an admin who lacks a permission becomes a user", Admin(Permission.ManageUsers()), ToUser, 1, null);
        yield return () => ("an admin who lacks a permission is made an admin again", Admin(Permission.ManageUsers()), ToAdmin, 1, null);
        yield return () => ("the only manager has the user role and loses a permission", MemberWithEveryPermission(), LosesAuditLog, 0, "permission");
        yield return () => ("the only manager has the user role and keeps the user role", MemberWithEveryPermission(), ToUser, 0, null);
    }

    [Test]
    [Property("Rule", "UM-4")]
    [MethodDataSource(nameof(Cases))]
    public void a_change_that_leaves_the_tenant_with_no_manager_is_refused(
        (string Name, User User, ManagerChange Change, int OtherManagers, string? RefusedField) row)
    {
        // Arrange
        var managerIds = (row.User.CanManageTenant ? [row.User.Id] : new List<string>())
            .Concat(Enumerable.Range(1, row.OtherManagers).Select(n => $"usr_other{n}"))
            .ToList();

        // Act
        var decision = TenantManagers.Decide(new ManagerCase(row.User, row.Change, managerIds));

        // Assert
        if (row.RefusedField is not null)
        {
            var refusal = decision.ShouldBeOfType<ManagerDecision.Refused>().Refusal.ShouldBeOfType<ManagerRefusal.LastManager>();
            refusal.Field.ShouldBe(row.RefusedField);
            refusal.Message.ShouldBe("This user is the only user who can manage the tenant. Make another user an admin first.");
        }
        else
        {
            var proof = decision.ShouldBeOfType<ManagerDecision.Kept>().Proof;
            proof.UserId.ShouldBe(row.User.Id);
            proof.Change.ShouldBe(row.Change);
        }
    }

    [Test]
    [Property("Rule", "UM-4")]
    public void a_user_can_manage_the_tenant_only_with_every_permission()
    {
        // Act
        var every = TenantManagers.CanManage(Permission.GetAll());
        var allButOne = TenantManagers.CanManage(Permission.GetAll().Where(p => p != Permission.ReadAgents()));

        // Assert
        every.ShouldBeTrue();
        allButOne.ShouldBeFalse();
        Admin().CanManageTenant.ShouldBeTrue();
        Member().CanManageTenant.ShouldBeFalse();
        MemberWithEveryPermission().CanManageTenant.ShouldBeTrue();
    }

    [Test]
    [Property("Rule", "UM-4")]
    public void a_tenant_starts_only_with_a_first_user_who_can_manage_it()
    {
        // Arrange
        var asAdmin = new FakeUserForCreation().Generate() with { Role = UserRole.Admin().Value };
        var asUser = new FakeUserForCreation().Generate() with { Role = UserRole.User().Value };

        // Act
        var admin = User.CreateFirstOfTenant(asAdmin);
        var act = () => User.CreateFirstOfTenant(asUser);

        // Assert
        admin.CanManageTenant.ShouldBeTrue();
        var refusal = act.ShouldThrow<FullstackTemplate.Server.Exceptions.ValidationException>();
        refusal.Property.ShouldBe("role");
        refusal.Message.ShouldBe("The first user of a tenant must be able to manage it. Start it as an admin.");
    }

    [Test]
    public void a_case_refuses_managers_that_do_not_match_the_permissions_of_the_user()
    {
        // Arrange
        var admin = Admin();
        var member = Member();

        // Act
        var managerNotListed = () => new ManagerCase(admin, Removal, ["usr_other"]);
        var memberListed = () => new ManagerCase(member, Removal, [member.Id]);

        // Assert
        managerNotListed.ShouldThrow<ArgumentException>();
        memberListed.ShouldThrow<ArgumentException>();
    }

    [Test]
    [Arguments(typeof(ManagerChange))]
    [Arguments(typeof(ManagerRefusal))]
    [Arguments(typeof(ManagerDecision))]
    public void each_kind_of_a_closed_type_is_nested_in_it(Type closed)
    {
        // Act
        var kinds = typeof(TenantManagers).Assembly.GetTypes().Where(t => t.IsSubclassOf(closed));

        // Assert
        kinds.ShouldAllBe(kind => kind.DeclaringType == closed);
    }

    private static User Admin(params Permission[] without)
    {
        var admin = User.Create(new FakeUserForCreation().Generate() with { Role = UserRole.Admin().Value });
        foreach (var permission in without)
            admin.RemovePermissionIgnoringLastManager(permission);
        return admin;
    }

    private static User Member() => User.Create(new FakeUserForCreation().Generate() with { Role = UserRole.User().Value });

    private static User MemberWithEveryPermission() => Member().AddPermissions(Permission.GetAll());
}
