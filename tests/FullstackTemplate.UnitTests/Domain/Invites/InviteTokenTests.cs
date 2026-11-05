namespace FullstackTemplate.UnitTests.Domain.Invites;

using System.Buffers.Text;
using FullstackTemplate.Server.Domain.Invites;
using Shouldly;

public class InviteTokenTests
{
    [Test, Property("Rule", "INV-2")]
    public void a_new_token_is_32_random_bytes_in_base64url()
    {
        // Act
        var tokens = Enumerable.Range(0, 100).Select(_ => InviteToken.New().Value).ToList();

        // Assert
        tokens.ShouldAllBe(t => t.Length == InviteToken.Length);
        tokens.ShouldAllBe(t => Base64Url.DecodeFromChars(t).Length == 32);
        tokens.Distinct().Count().ShouldBe(tokens.Count);
    }

    [Test, Property("Rule", "INV-2")]
    public void the_hash_is_the_hex_of_the_sha256_of_the_token()
    {
        // Act
        var hash = InviteToken.HashOf("abc");

        // Assert
        hash.ShouldBe("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
    }

    [Test, Property("Rule", "INV-2")]
    public void the_text_of_a_token_hides_its_value()
    {
        // Arrange
        var token = InviteToken.New();

        // Act
        var text = $"{token}";

        // Assert
        text.ShouldNotContain(token.Value);
    }
}
