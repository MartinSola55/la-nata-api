using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Template_Api.Audit.Services;
using Template_Api.Emailing.Models.Internal;
using Template_Api.Export.Services;
using Template_Api.Report.Services;
using Template_Api.Emailing.Services;
using Template_Api.MercadoPago.Models.Internal;
using Template_Api.MercadoPago.Services;
using Template_Api.WhatsApp.Models.Internal;
using Template_Api.WhatsApp.Services;

namespace Template_Api.Core.Services
{
    public class ServiceContainer
    {
        public static void AddServices(IServiceCollection services, IConfiguration config)
        {
            // Options
            services.Configure<EmailOptions>(config.GetSection("EmailOptions"));
            services.Configure<WhatsAppOptions>(config.GetSection("WhatsAppOptions"));
            services.Configure<MercadoPagoOptions>(config.GetSection("MercadoPagoOptions"));

            // AWS
            services.AddScoped<AwsS3Service>();

            // Audit
            services.AddScoped<AuditService>();

            // Documents
            services.AddScoped<ExportService>();
            services.AddScoped<ReportService>();

            // User and Auth
            services.AddScoped<TokenService>();
            services.AddScoped<AuthService>();
            services.AddScoped<UserService>();

            // Generic CRUDs
            services.AddScoped<GenericItemService>();
            services.AddScoped<ProductService>();
            services.AddScoped<CartService>();

            // Notifications
            services.AddScoped<EmailingService>();
            services.AddHttpClient<WhatsAppService>();

            // Payments
            services.AddHttpClient<MercadoPagoService>();
        }
    }
}
