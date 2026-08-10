using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Template_Api.Core.DAL.DB;
using Template_Api.Core.Models.Constants;
using Template_Api.Core.Models.DTO;
using Template_Api.Core.Models.DTO.Auth;

namespace Template_Api.Core.Services
{
    public class AuthService(TokenService tokenService, APIContext context)
    {
        private readonly TokenService _tokenService = tokenService;
        private readonly APIContext _db = context;

        public async Task<BaseResponse<LoginResponse>> Login(LoginRequest rq)
        {
            var rs = new BaseResponse<LoginResponse>();

            if (string.IsNullOrEmpty(rq.Email) || string.IsNullOrEmpty(rq.Password))
                return rs.SetError(Messages.Error.FieldsRequired(["Email", "Password"]));

            if (!ValidateEmail(rq.Email))
                return rs.SetError(Messages.Error.InvalidEmail());

            if (!ValidatePassword(rq.Password))
                return rs.SetError(Messages.Error.InvalidPassword());

            var email = rq.Email.ToLower();
            var user = await _db
                .User
                .Include(x => x.Role)
                .FirstOrDefaultAsync(x => x.Email == email);

            if (user == null || !ValidateHashedPassword(rq.Password, user.PasswordHash))
                return rs.SetError(Messages.Error.InvalidLogin());

            if (user.Role == null)
                return rs.SetError(Messages.Error.UserWithoutRole());

            var expiration = DateTime.UtcNow.AddDays(30);
            var token = _tokenService.GenerateToken(user, user.Role.Name, expiration);

            if (string.IsNullOrEmpty(token))
                return rs.SetError(Messages.Error.TokenCreation());

            rs.Data = new LoginResponse
            {
                Token = token,
                SessionExpiration = expiration,
                User = new LoginResponse.Item
                {
                    Id = user.Id,
                    Role = user.Role.Name,
                    Name = user.Name,
                    LastName = user.LastName,
                    Email = user.Email
                }
            };

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

        public bool ValidatePassword(string password)
        {
            return new bool[] {
                password.Length >= 8,
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
        private static bool ValidateHashedPassword(string password, string hashedPassword)
        {
            return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
        }

        private static bool ValidateEmail(string email)
        {
            return new EmailAddressAttribute().IsValid(email);
        }
        #endregion
    }
}
