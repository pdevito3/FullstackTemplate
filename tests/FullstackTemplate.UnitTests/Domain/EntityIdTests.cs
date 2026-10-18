namespace FullstackTemplate.UnitTests.Domain;

using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.SharedTestHelpers.Fakes.Tenant;
using FullstackTemplate.SharedTestHelpers.Fakes.User;
using Shouldly;

public class EntityIdTests
{
    [Fact]
    public void new_id_is_the_prefix_and_a_uuidv7()
    {
        // Act
        var id = EntityId.New("usr");

        // Assert
        id.ShouldMatch("^usr_[0-9a-f]{32}$");
        Guid.ParseExact(id["usr_".Length..], "N").Version.ShouldBe(7);
    }

    [Theory]
    [InlineData("Usr")]
    [InlineData("")]
    [InlineData("us1")]
    [InlineData("u_sr")]
    [InlineData("abcdefghijklmnopqr")]
    public void new_and_is_valid_reject_a_prefix_that_is_not_1_to_17_lower_case_letters(string prefix)
    {
        Should.Throw<ArgumentException>(() => EntityId.New(prefix));
        Should.Throw<ArgumentException>(() => EntityId.IsValid("usr_a", prefix));
    }

    [Fact]
    public void each_entity_gets_an_id_with_its_prefix()
    {
        // Act
        var tenant = Tenant.Create(new FakeTenantForCreation().Generate());
        var user = User.Create(new FakeUserForCreation().Generate() with { Role = UserRole.Admin() });

        // Assert
        tenant.Id.ShouldStartWith("ten_");
        user.Id.ShouldStartWith("usr_");
        user.UserPermissions.ShouldNotBeEmpty();
        user.UserPermissions.ShouldAllBe(p => p.Id.StartsWith("uperm_") && p.UserId == user.Id);
    }

    [Fact]
    public void tenant_uses_the_id_in_the_creation_model()
    {
        // Arrange
        var id = EntityId.New(Tenant.IdPrefix);

        // Act
        var tenant = Tenant.Create(new FakeTenantForCreation().Generate() with { Id = id });

        // Assert
        tenant.Id.ShouldBe(id);
    }

    [Fact]
    public void user_uses_the_id_in_the_creation_model_for_the_user_and_the_permissions()
    {
        // Arrange
        var id = EntityId.New(User.IdPrefix);

        // Act
        var user = User.Create(new FakeUserForCreation().Generate() with { Id = id, Role = UserRole.Admin() });

        // Assert
        user.Id.ShouldBe(id);
        user.UserPermissions.ShouldNotBeEmpty();
        user.UserPermissions.ShouldAllBe(p => p.UserId == id);
    }

    [Theory]
    [MemberData(nameof(BadIds), "ten")]
    public void tenant_rejects_an_id_without_the_tenant_prefix(string id)
    {
        // Act
        var act = () => Tenant.Create(new FakeTenantForCreation().Generate() with { Id = id });

        // Assert
        Should.Throw<ValidationException>(act).Message.ShouldContain("'ten_'");
    }

    [Theory]
    [MemberData(nameof(BadIds), "usr")]
    public void user_rejects_an_id_without_the_user_prefix(string id)
    {
        // Act
        var act = () => User.Create(new FakeUserForCreation().Generate() with { Id = id });

        // Assert
        Should.Throw<ValidationException>(act).Message.ShouldContain("'usr_'");
    }

    [Theory]
    [InlineData("usr_fixed")]
    [InlineData("usr_0199f0c1a2b37c4d8e9f0a1b2c3d4e5f")]
    [InlineData("usr_a")]
    public void is_valid_accepts_lower_case_letters_and_digits_after_the_prefix(string id)
    {
        EntityId.IsValid(id, "usr").ShouldBeTrue();
    }

    [Fact]
    public void is_valid_accepts_new_ids_and_the_default_test_tenant_id()
    {
        EntityId.IsValid(EntityId.New(User.IdPrefix), User.IdPrefix).ShouldBeTrue();
        EntityId.IsValid($"{Tenant.IdPrefix}_{new string('1', 32)}", Tenant.IdPrefix).ShouldBeTrue();
    }

    [Fact]
    public void is_valid_accepts_max_length_and_rejects_one_more()
    {
        // Arrange
        var maxLength = $"usr_{new string('a', EntityId.MaxLength - "usr_".Length)}";
        var tooLong = maxLength + "a";

        // Assert
        maxLength.Length.ShouldBe(EntityId.MaxLength);
        EntityId.IsValid(maxLength, "usr").ShouldBeTrue();
        EntityId.IsValid(tooLong, "usr").ShouldBeFalse();
    }

    [Theory]
    [InlineData("usr_0199f0c1a2b37c4d8e9f0a1b2c3d4e5f", "us")]
    [InlineData("ten_ ", "ten")]
    [InlineData("ten_ABC", "ten")]
    [InlineData("ten_abcé", "ten")]
    [InlineData("usr_a\0b", "usr")]
    [InlineData("usr_a-b", "usr")]
    [InlineData("usr_a_b", "usr")]
    [InlineData(null, "usr")]
    public void is_valid_rejects_other_prefixes_and_characters(string? id, string prefix)
    {
        EntityId.IsValid(id, prefix).ShouldBeFalse();
    }

    public static TheoryData<string> BadIds(string prefix) => new()
    {
        // The prefix of another entity type
        $"{(prefix == "usr" ? "ten" : "usr")}_0199f0c1a2b37c4d8e9f0a1b2c3d4e5f",
        // The prefix only
        $"{prefix}_",
        prefix,
        "",
        // More than EntityId.MaxLength characters
        $"{prefix}_{new string('a', EntityId.MaxLength)}",
    };
}
