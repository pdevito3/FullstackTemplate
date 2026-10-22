namespace FullstackTemplate.Server.Resources.Entrypoints.DPoP;

using Domain.DPoPProofs.Features;
using MediatR;

/// <summary>
/// The replay cache of the DPoP proofs: a table in <c>appdb</c>, so that all server instances share it.
/// </summary>
public interface IDPoPReplayCache
{
    /// <summary>
    /// Adds the proof. Returns false, and changes nothing, when the same key already used this <c>jti</c>.
    /// </summary>
    Task<bool> TryAddAsync(string jwkThumbprint, string jti, DateTimeOffset expires, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes at most <paramref name="batchSize"/> rows that expired at or before <paramref name="now"/>, and
    /// returns the number of rows that it deleted.
    /// </summary>
    Task<int> DeleteExpiredAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken);
}

/// <summary>
/// The replay cache through MediatR, as an adapter: the handlers read and write the database.
/// </summary>
public sealed class DPoPReplayCache(ISender sender) : IDPoPReplayCache
{
    public Task<bool> TryAddAsync(string jwkThumbprint, string jti, DateTimeOffset expires, CancellationToken cancellationToken) =>
        sender.Send(new RegisterDPoPProof.Command(jwkThumbprint, jti, expires), cancellationToken);

    public Task<int> DeleteExpiredAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken) =>
        sender.Send(new DeleteExpiredDPoPProofs.Command(now, batchSize), cancellationToken);
}
