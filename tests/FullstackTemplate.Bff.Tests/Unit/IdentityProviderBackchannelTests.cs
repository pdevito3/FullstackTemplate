namespace FullstackTemplate.Bff.Tests.Unit;

using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FullstackTemplate.Bff.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Configuration;
using Shouldly;

public class IdentityProviderBackchannelTests
{
    private static readonly X509Certificate2 Pinned = SelfSigned("CN=suite");
    private static readonly X509Certificate2 Other = SelfSigned("CN=other");

    [Test]
    public void a_certificate_that_passes_the_normal_checks_is_trusted()
    {
        // Act & Assert
        IdentityProviderBackchannel.IsTrusted(Other, SslPolicyErrors.None, Pinned).ShouldBeTrue();
    }

    [Test]
    public void the_pinned_certificate_is_trusted_with_chain_and_name_errors()
    {
        // Act & Assert
        IdentityProviderBackchannel.IsTrusted(
            Pinned, SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch, Pinned).ShouldBeTrue();
    }

    [Test]
    public void another_certificate_with_errors_is_not_trusted()
    {
        // Act & Assert
        IdentityProviderBackchannel.IsTrusted(Other, SslPolicyErrors.RemoteCertificateChainErrors, Pinned).ShouldBeFalse();
        IdentityProviderBackchannel.IsTrusted(null, SslPolicyErrors.RemoteCertificateNotAvailable, Pinned).ShouldBeFalse();
    }

    [Test]
    public void the_refresh_interval_setting_sets_only_the_refresh_interval()
    {
        // Arrange
        var options = new OpenIdConnectOptions();
        var automatic = options.AutomaticRefreshInterval;

        // Act
        IdentityProviderBackchannel.Configure(options, Configuration(new() { [IdentityProviderBackchannel.MetadataRefreshIntervalKey] = "00:00:01" }));

        // Assert
        options.RefreshInterval.ShouldBe(TimeSpan.FromSeconds(1));
        options.AutomaticRefreshInterval.ShouldBe(automatic);
        options.BackchannelHttpHandler.ShouldBeNull();
    }

    [Test]
    public void no_settings_keep_the_handler_defaults()
    {
        // Arrange
        var options = new OpenIdConnectOptions();
        var refresh = options.RefreshInterval;

        // Act
        IdentityProviderBackchannel.Configure(options, Configuration(new()));

        // Assert
        options.RefreshInterval.ShouldBe(refresh);
        options.BackchannelHttpHandler.ShouldBeNull();
    }

    [Test]
    public void a_pinned_certificate_path_sets_a_backchannel_handler()
    {
        // Arrange
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, Pinned.ExportCertificatePem());
            var options = new OpenIdConnectOptions();

            // Act
            IdentityProviderBackchannel.Configure(options, Configuration(new() { [IdentityProviderBackchannel.PinnedCertificatePathKey] = path }));

            // Assert
            options.BackchannelHttpHandler.ShouldBeOfType<SocketsHttpHandler>()
                .SslOptions.RemoteCertificateValidationCallback.ShouldNotBeNull();
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static IConfiguration Configuration(Dictionary<string, string?> settings) =>
        new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

    private static X509Certificate2 SelfSigned(string subject)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }
}
