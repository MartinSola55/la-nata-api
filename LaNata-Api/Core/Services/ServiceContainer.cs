using Microsoft.Extensions.DependencyInjection;
using LaNata_Api.Audit.Services;
using LaNata_Api.Export.Services;
using LaNata_Api.Report.Services;

namespace LaNata_Api.Core.Services
{
    public class ServiceContainer
    {
        public static void AddServices(IServiceCollection services)
        {
            // Audit
            services.AddScoped<AuditService>();

            // Documents
            services.AddScoped<ExportService>();
            services.AddScoped<ReportService>();

            // User and Auth
            services.AddScoped<TokenService>();
            services.AddScoped<AuthService>();
            services.AddScoped<UserService>();

            // Business
            services.AddScoped<CategoryService>();
            services.AddScoped<ProductService>();
            services.AddScoped<OrderService>();
            services.AddScoped<ExpenseService>();
            services.AddScoped<DashboardService>();
        }
    }
}
