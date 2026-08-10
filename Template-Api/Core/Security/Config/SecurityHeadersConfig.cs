using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using System.Threading.Tasks;

namespace Template_Api.Core.Security.Config
{
    public static class SecurityHeadersConfig
    {
        /// <summary>
        /// Prevents Kestrel from advertising the server and its version on every response.
        /// </summary>
        public static void HideServerHeader(IWebHostBuilder webHost)
        {
            webHost.ConfigureKestrel(options => options.AddServerHeader = false);
        }

        public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
        {
            return app.Use(async (context, next) =>
            {
                var headers = context.Response.Headers;

                headers["X-Content-Type-Options"] = "nosniff";
                headers["X-Frame-Options"] = "DENY";
                headers["Referrer-Policy"] = "no-referrer";
                headers["X-Permitted-Cross-Domain-Policies"] = "none";
                headers["Cross-Origin-Resource-Policy"] = "cross-origin";
                headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";
                headers["Cache-Control"] = "no-store";
                headers["Pragma"] = "no-cache";

                await next();
            });
        }
    }
}
