using System.ComponentModel.DataAnnotations;

namespace FullstackTemplate.Bff.Authentication;

/// <summary>
/// The settings of the refresh lock, from <c>Bff:Refresh</c>.
/// </summary>
public sealed class RefreshOptions : IValidatableObject
{
    public const string SectionName = "Bff:Refresh";

    /// <summary>
    /// <c>Bff:Refresh:LockTimeout</c>. The longest wait for the refresh lock of a BFF session. A wait that reaches it
    /// is a temporary refresh failure. The upper limit stays below the default command timeout of 30 seconds, so that
    /// the lock timeout, not the command timeout, ends a long wait.
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

    /// <summary>
    /// The time for the reread, the write, and the commit of a refresh, after the lock wait and the token request.
    /// </summary>
    public static readonly TimeSpan CommitMargin = TimeSpan.FromSeconds(2);

    /// <summary>
    /// The longest wait of a request for the refresh of another request in the same instance. That refresh can wait
    /// <see cref="LockTimeout"/> for the lock and then <see cref="TokenRequestTimeout"/> for the identity provider,
    /// so the wait is the sum of the two plus <see cref="CommitMargin"/>. A shorter wait can end before the refresh
    /// ends, and the request then gets a lock timeout although the refresh works. A wait that reaches it is a
    /// temporary refresh failure.
    /// </summary>
    public TimeSpan InProcessWaitLimit => LockTimeout + TokenRequestTimeout + CommitMargin;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (TokenRequestTimeout >= LockTimeout)
            yield return new ValidationResult(
                "Bff:Refresh:TokenRequestTimeout must be less than Bff:Refresh:LockTimeout.",
                [nameof(TokenRequestTimeout)]);
    }
}
