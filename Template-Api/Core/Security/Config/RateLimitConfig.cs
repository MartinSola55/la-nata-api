using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.RateLimiting;
using Template_Api.Core.Models.Constants;
using Template_Api.Core.Models.DTO;

namespace Template_Api.Core.Security.Config
{
    public class RateLimitConfig
    {
        private const int GlobalPermitLimit = 300;
        private const int GlobalWindowMinutes = 1;
        private const int LoginPermitLimit = 10;
        private const int LoginWindowMinutes = 5;

        public static void AddRateLimiting(IServiceCollection services)
        {
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                    BuildLimiter(context, GlobalPermitLimit, GlobalWindowMinutes));

                options.AddPolicy(RateLimitPolicies.Login, context => BuildLimiter(context, LoginPermitLimit, LoginWindowMinutes));

                options.OnRejected = async (context, cancellationToken) =>
                {
                    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                    var response = new BaseResponse().SetError(Messages.Error.TooManyRequests());
                    await context.HttpContext.Response.WriteAsJsonAsync(response, cancellationToken);
                };
            });
        }

        // Partitioned by client IP. Behind a reverse proxy this needs forwarded headers,
        // otherwise every request shares the proxy address as partition key.
        private static RateLimitPartition<string> BuildLimiter(HttpContext context, int permitLimit, int minutes)
        {
            var key = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromMinutes(minutes)
            });
        }
    }
}
