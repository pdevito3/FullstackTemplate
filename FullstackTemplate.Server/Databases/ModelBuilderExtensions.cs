namespace FullstackTemplate.Server.Databases;

using System.Linq.Expressions;
using Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Resources;

public static class ModelBuilderExtensions
{
    public static void FilterSoftDeletedRecords(this ModelBuilder modelBuilder)
    {
        Expression<Func<BaseEntity, bool>> filterExpr = e => !e.IsDeleted;
        foreach (var mutableEntityType in modelBuilder.Model.GetEntityTypes()
                     .Where(m => m.ClrType.IsAssignableTo(typeof(BaseEntity))))
        {
            // modify expression to handle correct child type
            var parameter = Expression.Parameter(mutableEntityType.ClrType);
            var body = ReplacingExpressionVisitor
                .Replace(filterExpr.Parameters.First(), parameter, filterExpr.Body);
            var lambdaExpression = Expression.Lambda(body, parameter);

            // set filter with name for selective disabling
            mutableEntityType.SetQueryFilter(QueryFilterNames.SoftDelete, lambdaExpression);
        }
    }
}
