namespace FullstackTemplate.Server.Resources.Jobs;

using Domain.Users;
using Microsoft.Extensions.Options;

/// <summary>Who can open the jobs dashboard. The dashboard and <c>GetCurrentUser</c> ask this one question.</summary>
public sealed class JobOperators(ICallerIdentityResolver callers, IOptions<BackgroundJobsOptions> options)
{
    /// <summary>
    /// True when the caller is a <see cref="CallerIdentity.User"/>, that is a token of a first-party client, and its
    /// <c>sub</c> is in <see cref="BackgroundJobsOptions.OperatorSubjects"/>. An agent or an all-tenants client is
    /// never an operator, also with the same <c>sub</c>.
    /// </summary>
    public async Task<bool> IncludesCallerAsync(CancellationToken cancellationToken)
        => await callers.GetAsync(cancellationToken) is CallerIdentity.User { Subject: var subject }
            && options.Value.OperatorSubjects.Contains(subject, StringComparer.Ordinal);
}
