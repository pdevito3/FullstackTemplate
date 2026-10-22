namespace FullstackTemplate.UnitTests.Resources;

using System.Text.Json;
using FullstackTemplate.Server.Resources.Entrypoints.DPoP;
using Shouldly;

public class DPoPProofValidatorTests
{
    [Test]
    public void the_thumbprint_of_the_example_key_of_RFC_7638_is_the_value_of_the_RFC()
    {
        // Arrange: the key of RFC 7638 section 3.1, without its kid, because a proof must not carry a kid
        const string jwk = """
            {
              "kty": "RSA",
              "n": "0vx7agoebGcQSuuPiLJXZptN9nndrQmbXEps2aiAFbWhM78LhWx4cbbfAAtVT86zwu1RK7aPFFxuhDR1L6tSoc_BJECPebWKRXjBZCiFV4n3oknjhMstn64tZ_2W-5JsGY4Hc5n9yBXArwl93lqt7_RN5w6Cf0h4QyQ5v-65YGjQR0_FDW2QvzqY368QQMicAtaSqzs8KJZgnYb9c7d0zgdAZHzu6qMQvRL5hajrn1n91CbOpbISD08qNLyrdkt-bFTWhAI4vMQFh6WeZu0fM4lFd2NcRwr3XPksINHaQ-G_xBniIqbw0Ls1jF44-csFCur-kEgU8awapJzKnqDKgw",
              "e": "AQAB",
              "alg": "RS256"
            }
            """;

        // Act
        var (key, error) = DPoPProofValidator.ReadPublicKey(JsonDocument.Parse(jwk).RootElement);

        // Assert
        error.ShouldBeNull();
        key.ShouldNotBeNull().Jkt.ShouldBe("NzbLsXh8uDCcd-6MNwXF4W_7noWXFZAfHkxZsRGC9Xs");
    }
}
