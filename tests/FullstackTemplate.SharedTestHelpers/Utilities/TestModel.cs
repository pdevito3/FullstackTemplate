namespace FullstackTemplate.SharedTestHelpers.Utilities;

using FullstackTemplate.Server.Domain.AuditEntries;
using FullstackTemplate.Server.Domain.Tenants;
using Microsoft.EntityFrameworkCore;

public static class TestModel
{
    /// <summary>
    /// Removes each entity type of the app from the model, except <see cref="Tenant"/>, <see cref="AuditEntry"/>, and
    /// the types in <paramref name="except"/>. A context of a test that adds its own entity types calls this after the base.
    /// </summary>
    public static ModelBuilder IgnoreTheAppEntities(this ModelBuilder modelBuilder, params Type[] except)
    {
        Type[] kept = [typeof(Tenant), typeof(AuditEntry), .. except];
        var appTypes = modelBuilder.Model.GetEntityTypes()
            .Where(e => !e.IsOwned() && !kept.Contains(e.ClrType))
            .Select(e => e.ClrType)
            .ToList();
        foreach (var type in appTypes)
            modelBuilder.Ignore(type);
        return modelBuilder;
    }
}
