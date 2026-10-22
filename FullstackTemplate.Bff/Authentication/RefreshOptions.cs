using System.ComponentModel.DataAnnotations;

namespace FullstackTemplate.Bff.Authentication;

/// <summary>
/// The settings of the refresh lock, from <c>Bff:Refresh</c>.
/// </summary>
public sealed class RefreshOptions : IValidatableObject
{
    public const string SectionName = "Bff:Refresh";

    /// <summary>
    /// <c>Bff:Refresh:LockTimeout</c>. The longest wait for the refresh lock of a BFF session, and the longest wait
    /// of a request for the refresh of another request in the same instance. A wait that reaches it is a temporary
    /// refresh failure. The upper limit stays below the default command timeout of 30 seconds, so that the lock
    /// timeout, not the command timeout, ends a long wait.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:00.100", "00:00:25")]
    public TimeSpan LockTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// <c>Bff:Refresh:TokenRequestTimeout</c>. The longest refresh call to the token endpoint of the identity
    /// provider. The refresh lock holds a <c>bffdb</c> connection during the call, so a hung identity provider must
    /// not hold it long. A call that reaches the limit is a temporary refresh failure. It must be less than
    /// <see cref="LockTimeout"/>, so that a request that waits for the lock gets the result of the refresh.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:00.100", "00:00:25")]
    public TimeSpan TokenRequestTimeout { get; set; } = TimeSpan.FromSeconds(5);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (TokenRequestTimeout >= LockTimeout)
            yield return new ValidationResult(
                "Bff:Refresh:TokenRequestTimeout must be less than Bff:Refresh:LockTimeout.",
                [nameof(TokenRequestTimeout)]);
    }
}
