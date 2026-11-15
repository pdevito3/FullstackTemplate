namespace FullstackTemplate.Server.Domain.Users;

using Databases;
using Microsoft.EntityFrameworkCore;

public static class AdminCaseLoader
{
    public static async Task<AdminCase> LoadAdminCaseAsync(this AppDbContext dbContext, User user, AdminChange change,
        CancellationToken cancellationToken)
    {
        var admin = UserRole.Admin().Value;
        var adminIds = await dbContext.Users
            .TagWithOperationCallSite("users.load_admins")
            .Where(u => u.TenantId == user.TenantId && u.Role.Value == admin)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);
        return new AdminCase(user, change, adminIds);
    }
}
