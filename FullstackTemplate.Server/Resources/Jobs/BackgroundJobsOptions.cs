namespace FullstackTemplate.Server.Resources.Jobs;

public sealed class BackgroundJobsOptions
{
    public const string SectionName = "BackgroundJobs";

    /// <summary>
    /// The <c>sub</c> claims of the users who can open the jobs dashboard. The dashboard shows the jobs of all
    /// tenants, so this is a power over all tenants, and ADR 0009 keeps such a power out of the tenant permissions.
    /// The default is empty, and then nobody can open the dashboard.
    /// </summary>
    public string[] OperatorSubjects { get; init; } = [];
}
