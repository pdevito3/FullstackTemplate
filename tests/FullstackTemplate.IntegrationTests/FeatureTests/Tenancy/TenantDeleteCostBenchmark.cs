namespace FullstackTemplate.IntegrationTests.FeatureTests.Tenancy;

using System.Diagnostics;
using System.Globalization;
using FullstackTemplate.Server.Domain.Tenants.Features;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Resources;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using Microsoft.EntityFrameworkCore;
using Shouldly;

public class TenantDeleteCostBenchmark : TestBase
{
    private static readonly TimeSpan SampleInterval = TimeSpan.FromMilliseconds(20);

    [Test]
    [Explicit]
    public async Task measure_the_delete_of_a_tenant_with_many_users()
    {
        // Arrange
        var users = int.Parse(Environment.GetEnvironmentVariable("TENANT_DELETE_BENCHMARK_USERS") ?? "1000", CultureInfo.InvariantCulture);
        var scope = new TestingServiceScope();
        await scope.StartGraph(out _)
            .AddTenant(out var tenant)
            .AddUser(out var admin, u => u.Role = UserRole.Admin().Value)
            .Under(tenant)
            .AddUsers(out _, users)
            .BuildAsync();
        scope.SetUserWithDefaults(identifier: admin.Identifier);
        var heapBefore = GC.GetTotalMemory(forceFullCollection: true);
        var allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
        using var sampling = new CancellationTokenSource();
        var peakHeap = SamplePeakHeapAsync(sampling.Token);

        // Act
        var clock = Stopwatch.StartNew();
        await scope.SendAsync(new DeleteTenant.Command(tenant.Id));
        clock.Stop();

        // Assert
        await sampling.CancelAsync();
        var peak = await peakHeap;
        var allocated = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
        var liveAfter = await new TestingServiceScope().ExecuteDbContextAsync(db => db.Users
            .IgnoreQueryFilters([QueryFilterNames.Tenant]).CountAsync(u => u.TenantId == tenant.Id));
        liveAfter.ShouldBe(0);
        Console.WriteLine(
            $"users={users + 1} delete_ms={clock.ElapsedMilliseconds} heap_before_mb={heapBefore / 1048576} "
            + $"peak_heap_mb={peak / 1048576} heap_growth_mb={(peak - heapBefore) / 1048576} allocated_mb={allocated / 1048576}");
    }

    private static async Task<long> SamplePeakHeapAsync(CancellationToken stop)
    {
        var peak = 0L;
        while (!stop.IsCancellationRequested)
        {
            peak = Math.Max(peak, GC.GetTotalMemory(forceFullCollection: false));
            await Task.Delay(SampleInterval, CancellationToken.None);
        }
        return peak;
    }
}
