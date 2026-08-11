using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace LaNata_Api.Core.Security.Config
{
    public class CorsConfig
    {
        public const string PolicyName = "DefaultCors";

        public static void AddCorsPolicy(IServiceCollection services, IConfiguration config)
        {
            var allowedOrigins = config["Cors:AllowedOrigins"]
                ?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (allowedOrigins == null || allowedOrigins.Length == 0)
                throw new InvalidOperationException("Cors:AllowedOrigins not found.");

            services.AddCors(options =>
            {
                options.AddPolicy(PolicyName, policy => policy
                    .WithOrigins(allowedOrigins)
                    .WithMethods("GET", "POST")
                    .AllowAnyHeader()
                    .WithExposedHeaders("Content-Disposition"));
            });
        }
    }
}
