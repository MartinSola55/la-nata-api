using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using LaNata_Api.Core.DAL.DB;
using LaNata_Api.Core.Security.Config;
using LaNata_Api.Report.Config;
using LaNata_Api.Core.Services;

var builder = WebApplication.CreateBuilder(args);

SecurityHeadersConfig.HideServerHeader(builder.WebHost);
ReportConfig.ConfigureReporting(builder.Configuration);

DatabaseConfig.AddDatabase(builder.Services, builder.Configuration);
ServiceContainer.AddServices(builder.Services);
CorsConfig.AddCorsPolicy(builder.Services, builder.Configuration);
RateLimitConfig.AddRateLimiting(builder.Services);
AuthenticationConfig.AddJwtAuthentication(builder.Services, builder.Configuration, builder.Environment);
AuthorizationConfig.AddAuthorizationPolicies(builder.Services);

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

await DatabaseConfig.RunMigrations(app.Services);

app.UseSecurityHeaders();

if (!app.Environment.IsDevelopment())
    app.UseHsts();

app.UseHttpsRedirection();
app.UseCors(CorsConfig.PolicyName);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
