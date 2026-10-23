namespace FullstackTemplate.UnitTests.Services;

using System.Security.Claims;
using FullstackTemplate.Server.Services;
using NSubstitute;
using Shouldly;

public class CurrentUserServiceTests
{
    private readonly ICurrentPrincipalAccessor _principalAccessor;
    private readonly CurrentUserService _sut;

    public CurrentUserServiceTests()
    {
        _principalAccessor = Substitute.For<ICurrentPrincipalAccessor>();
        _sut = new CurrentUserService(_principalAccessor);
    }

    private void SetupUserWithClaims(params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        _principalAccessor.Principal.Returns(principal);
    }

    private void SetupUnauthenticatedUser()
    {
        var identity = new ClaimsIdentity(); // No auth type = unauthenticated
        var principal = new ClaimsPrincipal(identity);
        _principalAccessor.Principal.Returns(principal);
    }

    [Test]
    public void User_returns_claims_principal_from_http_context()
    {
        // Arrange
        SetupUserWithClaims(new Claim(ClaimTypes.NameIdentifier, "user-123"));

        // Act
        var user = _sut.User;

        // Assert
        user.ShouldNotBeNull();
        user.Identity.ShouldNotBeNull();
        user.Identity!.IsAuthenticated.ShouldBeTrue();
    }

    [Test]
    public void User_returns_null_when_no_http_context()
    {
        // Arrange
        _principalAccessor.Principal.Returns((ClaimsPrincipal?)null);

        // Act
        var user = _sut.User;

        // Assert
        user.ShouldBeNull();
    }

    [Test]
    public void UserIdentifier_returns_sub_claim()
    {
        // Arrange
        SetupUserWithClaims(new Claim("sub", "user-123"));

        // Act
        var identifier = _sut.UserIdentifier;

        // Assert
        identifier.ShouldBe("user-123");
    }

    [Test]
    public void UserIdentifier_returns_name_identifier_claim_as_fallback()
    {
        // Arrange
        SetupUserWithClaims(new Claim(ClaimTypes.NameIdentifier, "user-456"));

        // Act
        var identifier = _sut.UserIdentifier;

        // Assert
        identifier.ShouldBe("user-456");
    }

    [Test]
    public void UserIdentifier_prefers_name_identifier_over_sub()
    {
        // Arrange - NameIdentifier is checked first in the implementation
        SetupUserWithClaims(
            new Claim(ClaimTypes.NameIdentifier, "name-id"),
            new Claim("sub", "sub-id"));

        // Act
        var identifier = _sut.UserIdentifier;

        // Assert
        identifier.ShouldBe("name-id");
    }

    [Test]
    public void Email_returns_email_claim()
    {
        // Arrange
        SetupUserWithClaims(new Claim(ClaimTypes.Email, "test@example.com"));

        // Act
        var email = _sut.Email;

        // Assert
        email.ShouldBe("test@example.com");
    }

    [Test]
    public void Email_returns_oidc_email_claim_as_fallback()
    {
        // Arrange
        SetupUserWithClaims(new Claim("email", "oidc@example.com"));

        // Act
        var email = _sut.Email;

        // Assert
        email.ShouldBe("oidc@example.com");
    }

    [Test]
    public void FirstName_returns_given_name_claim()
    {
        // Arrange
        SetupUserWithClaims(new Claim(ClaimTypes.GivenName, "John"));

        // Act
        var firstName = _sut.FirstName;

        // Assert
        firstName.ShouldBe("John");
    }

    [Test]
    public void FirstName_returns_oidc_given_name_as_fallback()
    {
        // Arrange
        SetupUserWithClaims(new Claim("given_name", "Jane"));

        // Act
        var firstName = _sut.FirstName;

        // Assert
        firstName.ShouldBe("Jane");
    }

    [Test]
    public void LastName_returns_surname_claim()
    {
        // Arrange
        SetupUserWithClaims(new Claim(ClaimTypes.Surname, "Doe"));

        // Act
        var lastName = _sut.LastName;

        // Assert
        lastName.ShouldBe("Doe");
    }

    [Test]
    public void LastName_returns_oidc_family_name_as_fallback()
    {
        // Arrange
        SetupUserWithClaims(new Claim("family_name", "Smith"));

        // Act
        var lastName = _sut.LastName;

        // Assert
        lastName.ShouldBe("Smith");
    }

    [Test]
    public void Name_returns_identity_name()
    {
        // Arrange
        SetupUserWithClaims(new Claim(ClaimTypes.Name, "John Doe"));

        // Act
        var name = _sut.Name;

        // Assert
        name.ShouldBe("John Doe");
    }

    [Test]
    public void Name_returns_oidc_name_claim_as_fallback()
    {
        // Arrange
        SetupUserWithClaims(new Claim("name", "Jane Smith"));

        // Act
        var name = _sut.Name;

        // Assert
        name.ShouldBe("Jane Smith");
    }

    [Test]
    public void Username_returns_preferred_username_claim()
    {
        // Arrange
        SetupUserWithClaims(new Claim("preferred_username", "johndoe"));

        // Act
        var username = _sut.Username;

        // Assert
        username.ShouldBe("johndoe");
    }

    [Test]
    public void Username_returns_username_claim_as_fallback()
    {
        // Arrange
        SetupUserWithClaims(new Claim("username", "janesmith"));

        // Act
        var username = _sut.Username;

        // Assert
        username.ShouldBe("janesmith");
    }

    [Test]
    public void ClientId_returns_client_id_claim()
    {
        // Arrange
        SetupUserWithClaims(new Claim("client_id", "my-client"));

        // Act
        var clientId = _sut.ClientId;

        // Assert
        clientId.ShouldBe("my-client");
    }

    [Test]
    public void ClientId_returns_azp_claim_for_keycloak()
    {
        // Arrange
        SetupUserWithClaims(new Claim("azp", "keycloak-client"));

        // Act
        var clientId = _sut.ClientId;

        // Assert
        clientId.ShouldBe("keycloak-client");
    }

    [Test]
    public void ClientId_ignores_the_non_standard_clientId_claim()
    {
        // Arrange
        SetupUserWithClaims(new Claim("clientId", "trusted-machine"));

        // Act
        var clientId = _sut.ClientId;

        // Assert
        clientId.ShouldBeNull();
    }

    [Test]
    public void ClientId_returns_applicationId_claim_for_fusionauth()
    {
        // Arrange
        SetupUserWithClaims(new Claim("applicationId", "fusionauth-app"));

        // Act
        var clientId = _sut.ClientId;

        // Assert
        clientId.ShouldBe("fusionauth-app");
    }

    [Test]
    public void ClientId_prefers_client_id_over_azp()
    {
        // Arrange
        SetupUserWithClaims(new Claim("azp", "authorized-party"), new Claim("client_id", "my-client"));

        // Act
        var clientId = _sut.ClientId;

        // Assert
        clientId.ShouldBe("my-client");
    }

    [Test]
    public void IsAuthenticated_returns_true_for_authenticated_user()
    {
        // Arrange
        SetupUserWithClaims(new Claim("sub", "user-123"));

        // Act
        var isAuthenticated = _sut.IsAuthenticated;

        // Assert
        isAuthenticated.ShouldBeTrue();
    }

    [Test]
    public void IsAuthenticated_returns_false_for_unauthenticated_user()
    {
        // Arrange
        SetupUnauthenticatedUser();

        // Act
        var isAuthenticated = _sut.IsAuthenticated;

        // Assert
        isAuthenticated.ShouldBeFalse();
    }

    [Test]
    public void Roles_returns_roles_from_role_claim()
    {
        // Arrange
        SetupUserWithClaims(
            new Claim("role", "Admin"),
            new Claim("role", "User"));

        // Act
        var roles = _sut.Roles;

        // Assert
        roles.ShouldContain("Admin");
        roles.ShouldContain("User");
    }

    [Test]
    public void Roles_returns_roles_from_roles_claim()
    {
        // Arrange
        SetupUserWithClaims(new Claim("roles", "Manager"));

        // Act
        var roles = _sut.Roles;

        // Assert
        roles.ShouldContain("Manager");
    }

    [Test]
    public void Roles_returns_roles_from_claim_types_role()
    {
        // Arrange
        SetupUserWithClaims(new Claim(ClaimTypes.Role, "SuperAdmin"));

        // Act
        var roles = _sut.Roles;

        // Assert
        roles.ShouldContain("SuperAdmin");
    }

    [Test]
    public void Roles_aggregates_from_multiple_claim_types()
    {
        // Arrange
        SetupUserWithClaims(
            new Claim("role", "Admin"),
            new Claim("roles", "Manager"),
            new Claim(ClaimTypes.Role, "User"));

        // Act
        var roles = _sut.Roles;

        // Assert
        roles.Count.ShouldBe(3);
        roles.ShouldContain("Admin");
        roles.ShouldContain("Manager");
        roles.ShouldContain("User");
    }

    [Test]
    public void Roles_returns_empty_when_no_user()
    {
        // Arrange
        _principalAccessor.Principal.Returns((ClaimsPrincipal?)null);

        // Act
        var roles = _sut.Roles;

        // Assert
        roles.ShouldBeEmpty();
    }

    [Test]
    public void IsInRole_returns_true_for_matching_role()
    {
        // Arrange
        SetupUserWithClaims(new Claim("role", "Admin"));

        // Act
        var isInRole = _sut.IsInRole("Admin");

        // Assert
        isInRole.ShouldBeTrue();
    }

    [Test]
    public void IsInRole_is_case_insensitive()
    {
        // Arrange
        SetupUserWithClaims(new Claim("role", "Admin"));

        // Act
        var isInRole = _sut.IsInRole("admin");

        // Assert
        isInRole.ShouldBeTrue();
    }

    [Test]
    public void IsInRole_returns_false_for_non_matching_role()
    {
        // Arrange
        SetupUserWithClaims(new Claim("role", "User"));

        // Act
        var isInRole = _sut.IsInRole("Admin");

        // Assert
        isInRole.ShouldBeFalse();
    }

    [Test]
    public void IsInRole_returns_false_when_no_user()
    {
        // Arrange
        _principalAccessor.Principal.Returns((ClaimsPrincipal?)null);

        // Act
        var isInRole = _sut.IsInRole("Admin");

        // Assert
        isInRole.ShouldBeFalse();
    }

    [Test]
    public void GetClaimValue_returns_claim_value()
    {
        // Arrange
        SetupUserWithClaims(new Claim("custom_claim", "custom_value"));

        // Act
        var value = _sut.GetClaimValue("custom_claim");

        // Assert
        value.ShouldBe("custom_value");
    }

    [Test]
    public void GetClaimValue_returns_null_for_missing_claim()
    {
        // Arrange
        SetupUserWithClaims(new Claim("other_claim", "value"));

        // Act
        var value = _sut.GetClaimValue("missing_claim");

        // Assert
        value.ShouldBeNull();
    }

    [Test]
    public void GetClaimValues_returns_all_values_for_claim_type()
    {
        // Arrange
        SetupUserWithClaims(
            new Claim("multi_claim", "value1"),
            new Claim("multi_claim", "value2"),
            new Claim("multi_claim", "value3"));

        // Act
        var values = _sut.GetClaimValues("multi_claim");

        // Assert
        values.Count.ShouldBe(3);
        values.ShouldContain("value1");
        values.ShouldContain("value2");
        values.ShouldContain("value3");
    }

    [Test]
    public void GetClaimValues_returns_empty_when_no_user()
    {
        // Arrange
        _principalAccessor.Principal.Returns((ClaimsPrincipal?)null);

        // Act
        var values = _sut.GetClaimValues("any_claim");

        // Assert
        values.ShouldBeEmpty();
    }
}
