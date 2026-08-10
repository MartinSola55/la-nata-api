using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Template_Api.Core.Models.Constants;

namespace Template_Api.Core.Security.Config
{
    public class AuthorizationConfig
    {
        public static void AddAuthorizationPolicies(IServiceCollection services)
        {
            services.AddAuthorizationBuilder()
                .AddPolicy(Policies.Admin, policy => policy.RequireRole(Roles.Admin))
                .AddPolicy(Policies.User, policy => policy.RequireRole(Roles.Admin, Roles.User));
        }
    }
}
