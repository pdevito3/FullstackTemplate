namespace FullstackTemplate.Server.Domain.Users;

using System.Diagnostics.Metrics;
using Resources;

public static class UserMetrics
{
    public static readonly Counter<long> RoleChanged = Telemetry.Meter.CreateCounter<long>(
        "app.users.role_changed",
        unit: "{user}",
        description: "Number of role changes, tagged with the new role");
}
