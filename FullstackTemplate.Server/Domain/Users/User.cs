namespace FullstackTemplate.Server.Domain.Users;

using EmailAddresses;
using DomainEvents;
using Models;

public class User : BaseEntity
{
    public string FirstName { get; private set; } = default!;
    public string LastName { get; private set; } = default!;
    public string Identifier { get; private set; } = default!;
    public EmailAddress Email { get; private set; } = default!;
    public string Username { get; private set; } = default!;

    public string FullName => $"{FirstName} {LastName}";

    public static User Create(UserForCreation userForCreation)
    {
        var user = new User
        {
            FirstName = userForCreation.FirstName,
            LastName = userForCreation.LastName,
            Identifier = userForCreation.Identifier,
            Email = EmailAddress.Of(userForCreation.Email),
            Username = userForCreation.Username
        };

        ValidateUser(user);
        user.QueueDomainEvent(new UserCreated(user));

        return user;
    }

    public User Update(UserForUpdate userForUpdate)
    {
        FirstName = userForUpdate.FirstName;
        LastName = userForUpdate.LastName;
        Email = EmailAddress.Of(userForUpdate.Email);
        Username = userForUpdate.Username;

        ValidateUser(this);
        QueueDomainEvent(new UserUpdated(Id));

        return this;
    }

    public User UpdateFromIdp(string firstName, string lastName, string email, string username)
    {
        FirstName = firstName;
        LastName = lastName;
        Email = EmailAddress.Of(email);
        Username = username;

        ValidateUser(this);
        QueueDomainEvent(new UserUpdated(Id));

        return this;
    }

    private static void ValidateUser(User user)
    {
        Exceptions.ValidationException.ThrowWhenNullOrWhitespace(user.FirstName, "Please provide a first name.");
        Exceptions.ValidationException.ThrowWhenNullOrWhitespace(user.LastName, "Please provide a last name.");
        Exceptions.ValidationException.ThrowWhenNullOrWhitespace(user.Identifier, "Please provide an identifier.");
        Exceptions.ValidationException.ThrowWhenNullOrWhitespace(user.Username, "Please provide a username.");
        Exceptions.ValidationException.ThrowWhenNull(user.Email, "Please provide a valid email.");
    }

    protected User() { } // For EF Core
}
