namespace FullstackTemplate.SharedTestHelpers.Fakes.User;

using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Domain.Users.Models;

public class FakeUserBuilder
{
    private UserForCreation _creationData = new FakeUserForCreation().Generate();

    public FakeUserBuilder WithFirstName(string firstName)
    {
        _creationData = _creationData with { FirstName = firstName };
        return this;
    }

    public FakeUserBuilder WithLastName(string lastName)
    {
        _creationData = _creationData with { LastName = lastName };
        return this;
    }

    public FakeUserBuilder WithIdentifier(string identifier)
    {
        _creationData = _creationData with { Identifier = identifier };
        return this;
    }

    public FakeUserBuilder WithEmail(string email)
    {
        _creationData = _creationData with { Email = email };
        return this;
    }

    public FakeUserBuilder WithUsername(string username)
    {
        _creationData = _creationData with { Username = username };
        return this;
    }

    public FakeUserBuilder WithRole(string role)
    {
        _creationData = _creationData with { Role = role };
        return this;
    }

    public FakeUserBuilder WithAdminRole()
    {
        _creationData = _creationData with { Role = "Admin" };
        return this;
    }

    public FakeUserBuilder WithUserRole()
    {
        _creationData = _creationData with { Role = "User" };
        return this;
    }

    public FakeUserBuilder WithTenantId(Guid tenantId)
    {
        _creationData = _creationData with { TenantId = tenantId };
        return this;
    }

    public User Build()
    {
        return User.Create(_creationData);
    }
}
