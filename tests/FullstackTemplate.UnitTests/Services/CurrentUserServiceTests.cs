namespace FullstackTemplate.UnitTests.Services;

using System.Security.Claims;
using FullstackTemplate.Server.Services;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Shouldly;

public class CurrentUserServiceTests
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly CurrentUserService _sut;

    public CurrentUserServiceTests()
    {
        _httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        _sut = new CurrentUserService(_httpContextAccessor);
    }

    private void SetupUserWithClaims(params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };
        _httpContextAccessor.HttpContext.Returns(httpContext);
    }

    private void SetupUnauthenticatedUser()
    {
        var identity = new ClaimsIdentity(); // No auth type = unauthenticated
        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };
        _httpContextAccessor.HttpContext.Returns(httpContext);
    }

    [Fact]
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

    [Fact]
    public void User_returns_null_when_no_http_context()
    {
        // Arrange
        _httpContextAccessor.HttpContext.Returns((HttpContext?)null);

        // Act
        var user = _sut.User;

        // Assert
        user.ShouldBeNull();
    }

    [Fact]
    public void UserIdentifier_returns_sub_claim()
    {
        // Arrange
        SetupUserWithClaims(new Claim("sub", "user-123"));

        // Act
        var identifier = _sut.UserIdentifier;

        // Assert
        identifier.ShouldBe("user-123");
    }

    [Fact]
    public void UserIdentifier_returns_name_identifier_claim_as_fallback()
    {
        // Arrange
        SetupUserWithClaims(new Claim(ClaimTypes.NameIdentifier, "user-456"));

        // Act
        var identifier = _sut.UserIdentifier;

        // Assert
        identifier.ShouldBe("user-456");
    }

    [Fact]
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

    [Fact]
    public void Email_returns_email_claim()
    {
        // Arrange
        SetupUserWithClaims(new Claim(ClaimTypes.Email, "test@example.com"));

        // Act
        var email = _sut.Email;

        // Assert
        email.ShouldBe("test@example.com");
    }

    [Fact]
    public void Email_returns_oidc_email_claim_as_fallback()
    {
        // Arrange
        SetupUserWithClaims(new Claim("email", "oidc@example.com"));

        // Act
        var email = _sut.Email;

        // Assert
        email.ShouldBe("oidc@example.com");
    }

    [Fact]
    public void FirstName_returns_given_name_claim()
    {
        // Arrange
        SetupUserWithClaims(new Claim(ClaimTypes.GivenName, "John"));

        // Act
        var firstName = _sut.FirstName;

        // Assert
        firstName.ShouldBe("John");
    }

    [Fact]
    public void FirstName_returns_oidc_given_name_as_fallback()
    {
        // Arrange
        SetupUserWithClaims(new Claim("given_name", "Jane"));

        // Act
        var firstName = _sut.FirstName;

        // Assert
        firstName.ShouldBe("Jane");
    }

    [Fact]
    public void LastName_returns_surname_claim()
    {
        // Arrange
        SetupUserWithClaims(new Claim(ClaimTypes.Surname, "Doe"));

        // Act
        var lastName = _sut.LastName;

        // Assert
        lastName.ShouldBe("Doe");
    }

    [Fact]
    public void LastName_returns_oidc_family_name_as_fallback()
    {
        // Arrange
        SetupUserWithClaims(new Claim("family_name", "Smith"));

        // Act
        var lastName = _sut.LastName;

        // Assert
        lastName.ShouldBe("Smith");
    }

    [Fact]
    public void Name_returns_identity_name()
    {
        // Arrange
        SetupUserWithClaims(new Claim(ClaimTypes.Name, "John Doe"));

        // Act
        var name = _sut.Name;

        // Assert
        name.ShouldBe("John Doe");
    }

    [Fact]
    public void Name_returns_oidc_name_claim_as_fallback()
    {
        // Arrange
        SetupUserWithClaims(new Claim("name", "Jane Smith"));

        // Act
        var name = _sut.Name;

        // Assert
        name.ShouldBe("Jane Smith");
    }

    [Fact]
    public void Username_returns_preferred_username_claim()
    {
        // Arrange
        SetupUserWithClaims(new Claim("preferred_username", "johndoe"));

        // Act
        var username = _sut.Username;

        // Assert
        username.ShouldBe("johndoe");
    }

    [Fact]
    public void Username_returns_username_claim_as_fallback()
    {
        // Arrange
        SetupUserWithClaims(new Claim("username", "janesmith"));

        // Act
        var username = _sut.Username;

        // Assert
        username.ShouldBe("janesmith");
    }

    [Fact]
    public void ClientId_returns_client_id_claim()
    {
        // Arrange
        SetupUserWithClaims(new Claim("client_id", "my-client"));

        // Act
        var clientId = _sut.ClientId;

        // Assert
        clientId.ShouldBe("my-client");
    }

    [Fact]
    public void ClientId_returns_azp_claim_for_keycloak()
    {
        // Arrange
        SetupUserWithClaims(new Claim("azp", "keycloak-client"));

        // Act
        var clientId = _sut.ClientId;

        // Assert
        clientId.ShouldBe("keycloak-client");
    }

    [Fact]
    public void IsMachine_returns_true_when_client_id_present_and_no_user_identifier()
    {
        // Arrange
        SetupUserWithClaims(new Claim("client_id", "machine-client"));

        // Act
        var isMachine = _sut.IsMachine;

        // Assert
        isMachine.ShouldBeTrue();
    }

    [Fact]
    public void IsMachine_returns_false_when_user_identifier_present()
    {
        // Arrange
        SetupUserWithClaims(
            new Claim("client_id", "some-client"),
            new Claim("sub", "user-123"));

        // Act
        var isMachine = _sut.IsMachine;

        // Assert
        isMachine.ShouldBeFalse();
    }

    [Fact]
    public void IsAuthenticated_returns_true_for_authenticated_user()
    {
        // Arrange
        SetupUserWithClaims(new Claim("sub", "user-123"));

        // Act
        var isAuthenticated = _sut.IsAuthenticated;

        // Assert
        isAuthenticated.ShouldBeTrue();
    }

    [Fact]
    public void IsAuthenticated_returns_false_for_unauthenticated_user()
    {
        // Arrange
        SetupUnauthenticatedUser();

        // Act
        var isAuthenticated = _sut.IsAuthenticated;

        // Assert
        isAuthenticated.ShouldBeFalse();
    }

    [Fact]
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

    [Fact]
    public void Roles_returns_roles_from_roles_claim()
    {
        // Arrange
        SetupUserWithClaims(new Claim("roles", "Manager"));

        // Act
        var roles = _sut.Roles;

        // Assert
        roles.ShouldContain("Manager");
    }

    [Fact]
    public void Roles_returns_roles_from_claim_types_role()
    {
        // Arrange
        SetupUserWithClaims(new Claim(ClaimTypes.Role, "SuperAdmin"));

        // Act
        var roles = _sut.Roles;

        // Assert
        roles.ShouldContain("SuperAdmin");
    }

    [Fact]
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

    [Fact]
    public void Roles_returns_empty_when_no_user()
    {
        // Arrange
        _httpContextAccessor.HttpContext.Returns((HttpContext?)null);

        // Act
        var roles = _sut.Roles;

        // Assert
        roles.ShouldBeEmpty();
    }

    [Fact]
    public void IsInRole_returns_true_for_matching_role()
    {
        // Arrange
        SetupUserWithClaims(new Claim("role", "Admin"));

        // Act
        var isInRole = _sut.IsInRole("Admin");

        // Assert
        isInRole.ShouldBeTrue();
    }

    [Fact]
    public void IsInRole_is_case_insensitive()
    {
        // Arrange
        SetupUserWithClaims(new Claim("role", "Admin"));

        // Act
        var isInRole = _sut.IsInRole("admin");

        // Assert
        isInRole.ShouldBeTrue();
    }

    [Fact]
    public void IsInRole_returns_false_for_non_matching_role()
    {
        // Arrange
        SetupUserWithClaims(new Claim("role", "User"));

        // Act
        var isInRole = _sut.IsInRole("Admin");

        // Assert
        isInRole.ShouldBeFalse();
    }

    [Fact]
    public void IsInRole_returns_false_when_no_user()
    {
        // Arrange
        _httpContextAccessor.HttpContext.Returns((HttpContext?)null);

        // Act
        var isInRole = _sut.IsInRole("Admin");

        // Assert
        isInRole.ShouldBeFalse();
    }

    [Fact]
    public void GetClaimValue_returns_claim_value()
    {
        // Arrange
        SetupUserWithClaims(new Claim("custom_claim", "custom_value"));

        // Act
        var value = _sut.GetClaimValue("custom_claim");

        // Assert
        value.ShouldBe("custom_value");
    }

    [Fact]
    public void GetClaimValue_returns_null_for_missing_claim()
    {
        // Arrange
        SetupUserWithClaims(new Claim("other_claim", "value"));

        // Act
        var value = _sut.GetClaimValue("missing_claim");

        // Assert
        value.ShouldBeNull();
    }

    [Fact]
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

    [Fact]
    public void GetClaimValues_returns_empty_when_no_user()
    {
        // Arrange
        _httpContextAccessor.HttpContext.Returns((HttpContext?)null);

        // Act
        var values = _sut.GetClaimValues("any_claim");

        // Assert
        values.ShouldBeEmpty();
    }
}
