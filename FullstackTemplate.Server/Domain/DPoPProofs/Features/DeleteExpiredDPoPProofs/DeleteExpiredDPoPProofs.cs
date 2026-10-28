namespace FullstackTemplate.Server.Domain.DPoPProofs.Features;

using System.Diagnostics.CodeAnalysis;
using Databases;
using MediatR;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Deletes at most <c>BatchSize</c> rows of the replay cache that expired at or before <c>Now</c>. The result is the
/// number of deleted rows.
/// </summary>
public static class DeleteExpiredDPoPProofs
{
    public sealed record Command(DateTimeOffset Now, int BatchSize) : IRequest<int>;

    public sealed class Handler(AppDbContext dbContext) : IRequestHandler<Command, int>
    {
        [SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs",
            Justification = "used_dpop_proofs is [NotAudited], so this write needs no audit entry.")]
        public Task<int> Handle(Command request, CancellationToken cancellationToken)
        {
            var batch = dbContext.Set<UsedDPoPProof>()
                .Where(p => p.Expires <= request.Now)
                .OrderBy(p => p.Id)
                .Take(request.BatchSize)
                .Select(p => p.Id);

            return dbContext.Set<UsedDPoPProof>()
                .TagWithOperationCallSite("dpop_used_proofs.delete_expired")
                .Where(p => batch.Contains(p.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }
    }
}
