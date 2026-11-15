namespace FullstackTemplate.Server.Domain.ObjectDeletes;

using System.Diagnostics.Metrics;
using Resources;

public static class ObjectDeleteMetrics
{
    public static readonly Counter<long> Swept = Telemetry.Meter.CreateCounter<long>(
        "app.objects.swept",
        unit: "{object}",
        description: "Number of objects that the sweep of the bucket deleted, because no row named them");
}
