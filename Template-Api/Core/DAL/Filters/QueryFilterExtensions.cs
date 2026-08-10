using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Linq.Expressions;
using Template_Api.Audit.Models;

namespace Template_Api.Core.DAL.Filters
{
    internal static class QueryFilterExtensions
    {
        private const string SOFT_DELETE_FILTER = "SoftDelete";
        private const string DELETED_AT_PROPERTY = nameof(AuditableEntity.DeletedAt);

        public static void ApplyGlobalFilters(this ModelBuilder builder)
        {
            foreach (var entityType in builder.Model.GetEntityTypes().Where(x => x.BaseType == null && !x.IsOwned()))
            {
                var clrType = entityType.ClrType;
                var entityBuilder = builder.Entity(clrType);

                if (TryCreateSoftDeleteFilter(clrType, out var softDeleteFilter))
                    entityBuilder.HasQueryFilter(SOFT_DELETE_FILTER, softDeleteFilter);
            }
        }

        private static bool TryCreateSoftDeleteFilter(Type entityType, out LambdaExpression filter)
        {
            filter = null;

            var deletedAt = entityType.GetProperty(DELETED_AT_PROPERTY);

            if (deletedAt == null || deletedAt.PropertyType != typeof(DateTime?))
                return false;

            var entity = Expression.Parameter(entityType, "x");
            var body = Expression.Equal(
                Expression.Property(entity, deletedAt),
                Expression.Constant(null, typeof(DateTime?)));

            filter = Expression.Lambda(body, entity);
            return true;
        }
    }
}
