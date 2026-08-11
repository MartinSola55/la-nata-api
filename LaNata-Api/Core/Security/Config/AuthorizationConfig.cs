using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using LaNata_Api.Core.Models.Constants;

namespace LaNata_Api.Core.Security.Config
{
    public class AuthorizationConfig
    {
        public static void AddAuthorizationPolicies(IServiceCollection services)
        {
            services.AddAuthorizationBuilder()
                .AddPolicy(Policies.Admin, policy => policy.RequireRole(Roles.Admin))
                .AddPolicy(Policies.Employee, policy => policy.RequireRole(Roles.Admin, Roles.Employee));
        }
    }
}
