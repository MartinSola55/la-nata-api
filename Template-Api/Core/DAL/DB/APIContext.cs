using Microsoft.EntityFrameworkCore;
using Template_Api.Audit.Models;
using Template_Api.Core.DAL.Filters;
using Template_Api.Core.Models;
using Template_Api.Emailing.Models;
using Template_Api.MercadoPago.Models;
using Template_Api.WhatsApp.Models;

namespace Template_Api.Core.DAL.DB
{
    public class APIContext(DbContextOptions<APIContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Product>()
                .Property(x => x.Price)
                .HasPrecision(18, 2);

            builder.Entity<Cart>()
                .Property(x => x.Total)
                .HasPrecision(18, 2);

            builder.Entity<CartProduct>()
                .Property(x => x.SettedPrice)
                .HasPrecision(18, 2);

            builder.Entity<MercadoPagoPayment>()
                .Property(x => x.TransactionAmount)
                .HasPrecision(18, 2);

            builder.ApplyGlobalFilters();
        }

        public DbSet<Migration> Migration { get; set; }
        public DbSet<EmailTemplate> EmailTemplate { get; set; }
        public DbSet<Role> Role { get; set; }
        public DbSet<User> User { get; set; }
        public DbSet<GenericItem> GenericItem { get; set; }
        public DbSet<Product> Product { get; set; }
        public DbSet<Cart> Cart { get; set; }
        public DbSet<CartProduct> CartProduct { get; set; }
        public DbSet<WhatsAppMessage> WhatsAppMessage { get; set; }
        public DbSet<MercadoPagoPayment> MercadoPagoPayment { get; set; }
        public DbSet<AuditCategory> AuditCategory { get; set; }
        public DbSet<AuditAction> AuditAction { get; set; }
        public DbSet<AuditEntry> AuditEntry { get; set; }
    }
}
