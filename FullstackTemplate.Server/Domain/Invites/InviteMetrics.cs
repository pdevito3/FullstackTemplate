namespace FullstackTemplate.Server.Domain.Invites;

using System.Diagnostics.Metrics;
using Resources;

public static class InviteMetrics
{
    public static readonly Counter<long> Claimed = Telemetry.Meter.CreateCounter<long>(
        "app.invites.claimed",
        unit: "{invite}",
        description: "Number of claimed invites, tagged with the role of the new user");
}
