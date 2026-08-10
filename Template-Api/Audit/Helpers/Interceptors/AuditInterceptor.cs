using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Template_Api.Audit.Models;
using Template_Api.Core.Services;

namespace Template_Api.Audit.Helpers.Interceptors
{
    public class AuditInterceptor(TokenService tokenService) : SaveChangesInterceptor
    {
        private const string CREATED_BY_FIELD = "CreatedBy";
        private const string UPDATED_AT_FIELD = "UpdatedAt";
        private const string UPDATED_BY_FIELD = "UpdatedBy";
        private const string DELETED_AT_FIELD = "DeletedAt";
        private const string DELETED_BY_FIELD = "DeletedBy";

        private readonly TokenService _tokenService = tokenService;

        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result)
        {
            StampAudit(eventData.Context);

            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            StampAudit(eventData.Context);

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private void StampAudit(DbContext context)
        {
            if (context == null)
                return;

            var entries = context
                .ChangeTracker
                .Entries<AuditableEntity>()
                .Where(x => x.State is EntityState.Added or EntityState.Modified)
                .ToList();

            if (entries.Count == 0)
                return;

            var utcNow = DateTime.UtcNow;
            var userId = _tokenService.TryGetToken()?.UserId;

            foreach (var entry in entries)
            {
                if (entry.State == EntityState.Added)
                {
                    if (userId != null)
                        entry.Property(CREATED_BY_FIELD).CurrentValue = userId;

                    continue;
                }

                var deletedAt = entry.Property(DELETED_AT_FIELD);
                var wasDeleted = deletedAt.IsModified && deletedAt.CurrentValue != null && deletedAt.OriginalValue == null;

                // A soft delete only stamps DeletedBy: UpdatedAt/UpdatedBy stay as they were
                if (wasDeleted)
                {
                    if (userId != null)
                        entry.Property(DELETED_BY_FIELD).CurrentValue = userId;

                    continue;
                }

                // A restore clears DeletedBy and then falls through as a regular update
                if (deletedAt.IsModified && deletedAt.CurrentValue == null)
                    entry.Property(DELETED_BY_FIELD).CurrentValue = null;

                entry.Property(UPDATED_AT_FIELD).CurrentValue = utcNow;

                if (userId != null)
                    entry.Property(UPDATED_BY_FIELD).CurrentValue = userId;
            }
        }
    }
}
