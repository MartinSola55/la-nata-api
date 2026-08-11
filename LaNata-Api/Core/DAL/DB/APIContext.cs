using Microsoft.EntityFrameworkCore;
using LaNata_Api.Audit.Models;
using LaNata_Api.Core.DAL.Filters;
using LaNata_Api.Core.Models;

namespace LaNata_Api.Core.DAL.DB
{
    // The model maps onto the legacy production schema: table and column names come from [Table] and
    // [Column] attributes on the entities, and money is double because the columns are float.
    public class APIContext(DbContextOptions<APIContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.ApplyGlobalFilters();
        }

        public DbSet<Migration> Migration { get; set; }
        public DbSet<Role> Role { get; set; }
        public DbSet<User> User { get; set; }
        public DbSet<Product> Product { get; set; }
        public DbSet<Order> Order { get; set; }
        public DbSet<ProductOrder> ProductOrder { get; set; }
        public DbSet<Expense> Expense { get; set; }
        public DbSet<AuditCategory> AuditCategory { get; set; }
        public DbSet<AuditAction> AuditAction { get; set; }
        public DbSet<AuditEntry> AuditEntry { get; set; }
    }
}
