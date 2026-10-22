namespace FullstackTemplate.IntegrationTests.Databases;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Domain.Tenants.Models;
using FullstackTemplate.Server.Resources.Entrypoints;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Shouldly;

/// <summary>
/// The schema has no exclusion constraint yet, so the test makes one on a temporary copy of the tenants table. A
/// temporary table comes first in the search path of its session, and EF writes unqualified table names, so this
/// context saves to the copy. Other tests do not see the copy, and the transaction drops it.
/// </summary>
public class ExclusionViolationTests : TestBase
{
    [Test]
    public async Task overlapping_save_is_an_overlap_conflict()
    {
        var dbContext = new TestingServiceScope().GetService<AppDbContext>();

        // The retrying execution strategy runs a user transaction only as one unit
        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            // Arrange
            await using var transaction = await dbContext.Database.BeginTransactionAsync();
            await dbContext.Database.ExecuteSqlRawAsync(
                "CREATE TEMP TABLE tenants (LIKE public.tenants INCLUDING DEFAULTS) ON COMMIT DROP");
            await dbContext.Database.ExecuteSqlRawAsync(
                "ALTER TABLE tenants ADD CONSTRAINT ex_tenants_name EXCLUDE USING btree (name WITH =)");
            var name = $"Tenant {Guid.NewGuid():N}";
            dbContext.Tenants.Add(Tenant.Create(new TenantForCreation { Name = name }));
            dbContext.Tenants.Add(Tenant.Create(new TenantForCreation { Name = name }));

            // Act
            Func<Task> act = () => dbContext.SaveChangesAsync();

            // Assert
            var exception = await act.ShouldThrowAsync<ConstraintConflictException>();
            exception.Constraint.ShouldBe("ex_tenants_name");
            exception.Message.ShouldBe(ConstraintConflictException.OverlapMessage);
            DomainProblemDetails.StatusCodeFor(exception).ShouldBe(StatusCodes.Status409Conflict);
            await transaction.RollbackAsync();
        });
    }
}
