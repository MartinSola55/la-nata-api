using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using Template_Api.Audit.Helpers.Interceptors;

namespace Template_Api.Core.Helpers.Interceptors
{
    public class InterceptorsContainer
    {
        public static void AddInterceptors(IServiceCollection services)
        {
            // Register all interceptors here
            services.AddScoped<AuditInterceptor>();
        }

        public static void ConfigureInterceptors(IServiceProvider serviceProvider, DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.AddInterceptors(
                serviceProvider.GetRequiredService<AuditInterceptor>()
            );
        }
    }
}
