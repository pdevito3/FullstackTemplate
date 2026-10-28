namespace FullstackTemplate.Server.Domain.DPoPProofs.Features;

using System.Diagnostics.CodeAnalysis;
using Databases;
using MediatR;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Adds the proof to the replay cache. The result is false when the same key already used this <c>jti</c>.
/// </summary>
public static class RegisterDPoPProof
{
    public sealed record Command(string JwkThumbprint, string Jti, DateTimeOffset Expires) : IRequest<bool>;

    public sealed class Handler(AppDbContext dbContext) : IRequestHandler<Command, bool>
    {
        [SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs",
            Justification = "used_dpop_proofs is [NotAudited], so this write needs no audit entry.")]
        public async Task<bool> Handle(Command request, CancellationToken cancellationToken)
        {
            var id = UsedDPoPProof.KeyFor(request.JwkThumbprint, request.Jti);
            // EF Core has no insert that ignores a duplicate key, and a failed SaveChanges logs an error
            return await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                -- dpop_used_proofs.add
                INSERT INTO used_dpop_proofs (id, expires) VALUES ({id}, {request.Expires.ToUniversalTime()})
                ON CONFLICT (id) DO NOTHING
                """,
                cancellationToken) > 0;
        }
    }
}
