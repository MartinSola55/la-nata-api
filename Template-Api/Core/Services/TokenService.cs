using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using Template_Api.Core.Models;
using Template_Api.Core.Models.Internal;

namespace Template_Api.Core.Services
{
    public class TokenService(IHttpContextAccessor httpContextAccessor, IConfiguration config)
    {
        private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
        private readonly IConfiguration _config = config;

        public Token GetToken()
        {
            return TryGetToken() ?? throw new Exception("No se ha podido encontrar el token");
        }

        public Token TryGetToken()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
                return null;

            var userIdClaim = user.Claims.FirstOrDefault(x => x.Type == "userId")?.Value;
            var email = user.Claims.FirstOrDefault(x => x.Type == ClaimTypes.Email)?.Value;
            var name = user.Claims.FirstOrDefault(x => x.Type == ClaimTypes.Name)?.Value;
            var lastName = user.Claims.FirstOrDefault(x => x.Type == ClaimTypes.Surname)?.Value;
            var role = user.Claims.FirstOrDefault(x => x.Type == ClaimTypes.Role)?.Value;

            if (!int.TryParse(userIdClaim, out var userId) || email == null || name == null || lastName == null || role == null)
                return null;

            return new Token
            {
                UserId = userId,
                Email = email,
                Name = name,
                LastName = lastName,
                Role = role
            };
        }

        public string GenerateToken(User user, string role, DateTime expiration)
        {
            var jwtKey = _config["Jwt:Key"];

            if (string.IsNullOrEmpty(jwtKey))
                return null;

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.Name),
                new Claim(ClaimTypes.Surname, user.LastName),
                new Claim("userId", user.Id.ToString()),
                new Claim(ClaimTypes.Role, role)
            };

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims,
                expires: expiration,
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
