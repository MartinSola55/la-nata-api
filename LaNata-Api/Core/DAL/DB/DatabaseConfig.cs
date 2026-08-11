using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using LaNata_Api.Core.Helpers.Interceptors;

namespace LaNata_Api.Core.DAL.DB
{
    public class DatabaseConfig
    {
        public static void AddDatabase(IServiceCollection services, IConfiguration config)
        {
            var connectionString = config.GetConnectionString("APIContextConnection")
                ?? throw new InvalidOperationException("Connection string 'APIContextConnection' not found.");

            services.AddScoped<MigrationRunner>();

            InterceptorsContainer.AddInterceptors(services);

            services.AddDbContext<APIContext>((serviceProvider, options) =>
            {
                options.UseSqlServer(connectionString);
                InterceptorsContainer.ConfigureInterceptors(serviceProvider, options);
            });
        }

        public static async Task RunMigrations(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var runner = scope.ServiceProvider.GetRequiredService<MigrationRunner>();
            await runner.Run();
        }
    }
}
