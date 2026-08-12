using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using LaNata_Api.Audit.Models.Constants;
using LaNata_Api.Audit.Models.Internal;
using LaNata_Api.Audit.Services;
using LaNata_Api.Core.DAL.DB;
using LaNata_Api.Core.Models.Constants;
using LaNata_Api.Core.Models.DTO;
using LaNata_Api.Core.Models.DTO.Auth;

namespace LaNata_Api.Core.Services
{
    public class AuthService(TokenService tokenService, APIContext context, AuditService auditService)
    {
        private readonly TokenService _tokenService = tokenService;
        private readonly APIContext _db = context;
        private readonly AuditService _auditService = auditService;

        public async Task<BaseResponse<LoginResponse>> Login(LoginRequest rq)
        {
            var rs = new BaseResponse<LoginResponse>();

            if (rq == null || string.IsNullOrEmpty(rq.Email) || string.IsNullOrEmpty(rq.Password))
            {
                await AuditLoginFailed(rq?.Email, null, "Campos requeridos incompletos");
                return rs.SetError(Messages.Error.FieldsRequired(["Email", "Password"]));
            }

            if (!ValidateEmail(rq.Email))
            {
                await AuditLoginFailed(rq.Email, null, "Email inválido");
                return rs.SetError(Messages.Error.InvalidEmail());
            }

            if (rq.Email.Length > ValidationLimits.UserEmailMaxLength)
            {
                await AuditLoginFailed(rq.Email, null, "Email demasiado largo");
                return rs.SetError(Messages.Error.FieldTooLong("email", ValidationLimits.UserEmailMaxLength));
            }

            if (rq.Password.Length > ValidationLimits.PasswordMaxLength)
            {
                await AuditLoginFailed(rq.Email, null, "Contraseña demasiado larga");
                return rs.SetError(Messages.Error.FieldTooLong("contraseña", ValidationLimits.PasswordMaxLength));
            }

            var email = rq.Email.ToLower();
            var user = await _db
                .User
                .Include(x => x.Role)
                .FirstOrDefaultAsync(x => x.Email == email);

            if (user == null || !VerifyPassword(rq.Password, user.PasswordHash))
            {
                await AuditLoginFailed(email, user?.Id, "Credenciales inválidas");
                return rs.SetError(Messages.Error.InvalidLogin());
            }

            if (user.Role == null)
            {
                await AuditLoginFailed(email, user.Id, "Usuario sin rol");
                return rs.SetError(Messages.Error.UserWithoutRole());
            }

            var expiration = DateTime.UtcNow.AddDays(30);
            var token = _tokenService.GenerateToken(user, user.Role.Name, expiration);

            if (string.IsNullOrEmpty(token))
            {
                await AuditLoginFailed(email, user.Id, "No se pudo generar el token");
                return rs.SetError(Messages.Error.TokenCreation());
            }

            rs.Data = new LoginResponse
            {
                Token = token,
                SessionExpiration = expiration,
                User = new LoginResponse.Item
                {
                    Id = user.Id,
                    Role = user.Role.Name,
                    Email = user.Email
                }
            };

            await AuditLogin(user.Id, user.Email, user.Role.Name, expiration);

            return rs;
        }

        public BaseResponse Logout()
        {
            var rs = new BaseResponse();

            if (_tokenService.TryGetToken() == null)
                return rs.SetError(Messages.Error.ExpiredToken());

            return rs;
        }

        public string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        public bool VerifyPassword(string password, string hashedPassword)
        {
            if (string.IsNullOrWhiteSpace(hashedPassword))
                return false;

            try
            {
                return BCrypt.Net.BCrypt.Verify(password, hashedPassword.TrimEnd());
            }
            catch (BCrypt.Net.SaltParseException)
            {
                return false;
            }
        }

        public bool ValidatePassword(string password)
        {
            if (string.IsNullOrEmpty(password))
                return false;

            return new bool[] {
                password.Length >= ValidationLimits.PasswordMinLength,
                password.Length <= ValidationLimits.PasswordMaxLength,
                password.Any(char.IsUpper),
                password.Any(char.IsLower),
                password.Any(char.IsDigit),
            }.All(x => x);
        }

        public bool IsAdmin()
        {
            return _tokenService.TryGetToken()?.Role == Roles.Admin;
        }

        #region Private
        private static bool ValidateEmail(string email)
        {
            return new EmailAddressAttribute().IsValid(email);
        }

        private async Task AuditLogin(int userId, string email, string role, DateTime expiration)
        {
            await _auditService.Audit(new AuditRequest
            {
                ActionCode = AuditActionCode.LOGIN,
                Entity = AuditEntityCode.USER,
                EntityKey = userId.ToString(),
                Name = email,
                Description = "Inicio de sesión exitoso",
                UserId = userId,
                UserName = email,
                UserEmail = email,
                Payload = new Dictionary<string, object>
                {
                    ["Email"] = email,
                    ["Rol"] = role,
                    ["Expira"] = expiration
                }
            });
        }

        private async Task AuditLoginFailed(string email, int? userId, string reason)
        {
            var cleanEmail = email?.Trim().ToLower();

            await _auditService.Audit(new AuditRequest
            {
                ActionCode = AuditActionCode.LOGIN_FAILED,
                Entity = AuditEntityCode.USER,
                EntityKey = userId?.ToString(),
                Name = string.IsNullOrWhiteSpace(cleanEmail) ? "Login sin email" : cleanEmail,
                Description = "Intento de inicio de sesión fallido",
                UserId = userId,
                UserName = cleanEmail,
                UserEmail = cleanEmail,
                Payload = new Dictionary<string, object>
                {
                    ["Email"] = string.IsNullOrWhiteSpace(cleanEmail) ? "Sin informar" : cleanEmail,
                    ["Motivo"] = reason
                }
            });
        }
        #endregion
    }
}
