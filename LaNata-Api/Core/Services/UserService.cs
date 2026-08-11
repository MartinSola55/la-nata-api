using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using LaNata_Api.Core.DAL.DB;
using LaNata_Api.Core.Models;
using LaNata_Api.Core.Models.Constants;
using LaNata_Api.Core.Models.DTO;
using LaNata_Api.Core.Models.DTO.User;

namespace LaNata_Api.Core.Services
{
    public class UserService(APIContext context, TokenService tokenService, AuthService authService)
    {
        private const int EmailMaxLength = 60;

        private readonly APIContext _db = context;
        private readonly TokenService _tokenService = tokenService;
        private readonly AuthService _authService = authService;

        #region Combos
        public async Task<BaseResponse<BaseComboResponse>> GetComboRoles()
        {
            return new BaseResponse<BaseComboResponse>
            {
                Data = new BaseComboResponse
                {
                    Items = await _db.Role
                        .OrderBy(x => x.Name)
                        .Select(x => new BaseComboResponse.Item
                        {
                            Id = x.Id,
                            Description = x.Name
                        })
                        .ToListAsync()
                }
            };
        }

        public async Task<BaseResponse<BaseComboResponse>> GetComboUsers(GetComboUsersRequest rq)
        {
            var token = _tokenService.GetToken();
            var query = _db
                .User
                .OrderBy(x => x.Email)
                .AsQueryable();

            if (!_authService.IsAdmin())
                query = query.Where(x => x.Id == token.UserId);
            else if (rq.UserId.HasValue)
                query = query.Where(x => x.Id == rq.UserId.Value);

            return new BaseResponse<BaseComboResponse>
            {
                Data = new BaseComboResponse
                {
                    Items = await query
                        .Select(x => new BaseComboResponse.Item
                        {
                            Id = x.Id,
                            Description = x.Email
                        })
                        .ToListAsync()
                }
            };
        }
        #endregion

        #region CRUD
        public async Task<BaseResponse<GetAllResponse>> GetAll(GetAllRequest rq)
        {
            rq ??= new GetAllRequest();

            var query = _db.User.AsQueryable();

            query = FilterQuery(query, rq);
            query = OrderQuery(query, rq);

            return new BaseResponse<GetAllResponse>
            {
                Data = new GetAllResponse
                {
                    Items = await query
                        .Select(x => new GetAllResponse.Item
                        {
                            Id = x.Id,
                            Email = x.Email,
                            RoleId = x.RoleId,
                            CreatedAt = x.CreatedAt,
                            Role = x.Role.Name
                        })
                        .Skip((rq.Page - 1) * Pagination.DefaultPageSize)
                        .Take(Pagination.DefaultPageSize)
                        .ToListAsync()
                }
            };
        }

        public async Task<BaseResponse<GetOneResponse>> GetOne(GetOneRequest rq)
        {
            var rs = new BaseResponse<GetOneResponse>();
            var token = _tokenService.GetToken();

            if (!_authService.IsAdmin() && rq.Id != token.UserId)
                return rs.SetError(Messages.Error.Unauthorized());

            var user = await _db
                .User
                .Include(x => x.Role)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == rq.Id);

            if (user == null)
                return rs.SetError(Messages.Error.EntityNotFound("Usuario"));

            rs.Data = new GetOneResponse
            {
                Id = user.Id,
                Email = user.Email,
                RoleId = user.RoleId,
                Role = user.Role.Name,
                CreatedAt = user.CreatedAt
            };

            return rs;
        }

        public async Task<BaseResponse<CreateResponse>> Create(CreateRequest rq)
        {
            var rs = new BaseResponse<CreateResponse>();

            if (rq == null)
                return rs.SetError(Messages.Error.FieldsRequired());

            var role = await _db.Role.FirstOrDefaultAsync(x => x.Id == rq.RoleId);
            if (role == null)
                return rs.SetError(Messages.Error.EntityNotFound("Rol"));

            var user = new User
            {
                Email = rq.Email?.ToLower() ?? string.Empty,
                PasswordHash = string.IsNullOrEmpty(rq.Password) ? string.Empty : _authService.HashPassword(rq.Password),
                RoleId = role.Id
            };

            if (!rs.Attach(await ValidateUser<CreateResponse>(user, rq.Password)).Success)
                return rs;

            try
            {
                _db.User.Add(user);
                await _db.SaveChangesAsync();
            }
            catch (Exception)
            {
                return rs.SetError(Messages.Error.Exception());
            }

            rs.Message = Messages.CRUD.EntityCreated("Usuario");
            rs.Data = new CreateResponse
            {
                Id = user.Id,
                Email = user.Email,
                Role = role.Name
            };

            return rs;
        }

        public async Task<BaseResponse<UpdateResponse>> Update(UpdateRequest rq)
        {
            var rs = new BaseResponse<UpdateResponse>();
            var token = _tokenService.GetToken();

            if (rq == null || rq.Id <= 0)
                return rs.SetError(Messages.Error.FieldGraterThanZero("id"));

            if (!_authService.IsAdmin() && rq.Id != token.UserId)
                return rs.SetError(Messages.Error.Unauthorized());

            var user = await _db.User.FirstOrDefaultAsync(x => x.Id == rq.Id);
            if (user == null)
                return rs.SetError(Messages.Error.EntityNotFound("Usuario"));

            var roleId = _authService.IsAdmin() ? rq.RoleId : user.RoleId;
            var role = await _db.Role.FirstOrDefaultAsync(x => x.Id == roleId);
            if (role == null)
                return rs.SetError(Messages.Error.EntityNotFound("Rol"));

            user.Email = rq.Email?.ToLower() ?? string.Empty;
            user.RoleId = role.Id;
            user.UpdatedAt = DateTime.UtcNow;

            var checkPassword = !string.IsNullOrEmpty(rq.Password);
            if (checkPassword)
                user.PasswordHash = _authService.HashPassword(rq.Password!);

            if (!rs.Attach(await ValidateUser<UpdateResponse>(user, rq.Password, checkPassword)).Success)
                return rs;

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (Exception)
            {
                return rs.SetError(Messages.Error.Exception());
            }

            rs.Message = Messages.CRUD.EntityUpdated("Usuario");
            rs.Data = new UpdateResponse
            {
                Id = user.Id,
                Email = user.Email,
                Role = role.Name
            };

            return rs;
        }

        public async Task<BaseResponse> Delete(DeleteRequest rq)
        {
            var rs = new BaseResponse();
            var token = _tokenService.GetToken();

            if (rq == null || rq.Id <= 0)
                return rs.SetError(Messages.Error.FieldGraterThanZero("id"));

            if (rq.Id == token.UserId)
                return rs.SetError(Messages.Error.InvalidField("usuario"));

            var user = await _db
                .User
                .Include(x => x.Role)
                .FirstOrDefaultAsync(x => x.Id == rq.Id);

            if (user == null)
                return rs.SetError(Messages.Error.EntityNotFound("Usuario"));

            if (user.Role.Name == Roles.Admin && !await _db.User.AnyAsync(x => x.Id != user.Id && x.Role.Name == Roles.Admin))
                return rs.SetError(Messages.Error.InvalidField("usuario"));

            user.DeletedAt = DateTime.UtcNow;

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (Exception)
            {
                return rs.SetError(Messages.Error.Exception());
            }

            rs.Message = Messages.CRUD.EntityDeleted("Usuario");
            return rs;
        }
        #endregion

        #region Methods
        public async Task<BaseResponse> UpdatePassword(UpdatePasswordRequest rq)
        {
            var rs = new BaseResponse();
            var token = _tokenService.GetToken();

            if (rq == null)
                return rs.SetError(Messages.Error.FieldsRequired());

            if (!_authService.IsAdmin() && rq.Id.HasValue && rq.Id.Value != token.UserId)
                return rs.SetError(Messages.Error.Unauthorized());

            if (string.IsNullOrEmpty(rq.Password))
                return rs.SetError(Messages.Error.FieldsRequired());

            if (!_authService.ValidatePassword(rq.Password))
                return rs.SetError(Messages.Error.InvalidPassword());

            var userId = rq.Id ?? token.UserId;
            var user = await _db.User.FirstOrDefaultAsync(x => x.Id == userId);
            if (user == null)
                return rs.SetError(Messages.Error.EntityNotFound("Usuario"));

            user.PasswordHash = _authService.HashPassword(rq.Password);
            user.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (Exception)
            {
                return rs.SetError(Messages.Error.Exception());
            }

            rs.Message = Messages.CRUD.EntityUpdated("Contraseña");
            return rs;
        }
        #endregion

        #region Validations
        private async Task<BaseResponse<T>> ValidateUser<T>(User user, string password, bool checkPassword = true)
        {
            var rs = new BaseResponse<T>();

            if (string.IsNullOrEmpty(user.Email))
                return rs.SetError(Messages.Error.FieldsRequired());

            if (!ValidateEmail(user.Email))
                return rs.SetError(Messages.Error.InvalidEmail());

            if (user.Email.Length > EmailMaxLength)
                return rs.SetError(Messages.Error.FieldTooLong("email", EmailMaxLength));

            if (checkPassword && (string.IsNullOrEmpty(password) || !_authService.ValidatePassword(password)))
                return rs.SetError(Messages.Error.InvalidPassword());

            if (await _db.User.AnyAsync(x => x.Email.ToLower() == user.Email.ToLower() && x.Id != user.Id))
                return rs.SetError(Messages.Error.DuplicateEmail());

            return rs;
        }

        private static bool ValidateEmail(string email)
        {
            return new EmailAddressAttribute().IsValid(email);
        }
        #endregion

        #region Helpers
        private IQueryable<User> FilterQuery(IQueryable<User> query, GetAllRequest rq)
        {
            if (rq.DateFrom.HasValue && rq.DateTo.HasValue && rq.DateFrom <= rq.DateTo)
            {
                var dateFrom = rq.DateFrom.Value.Date;
                var dateTo = rq.DateTo.Value.Date.AddDays(1);

                query = query.Where(x => x.CreatedAt >= dateFrom && x.CreatedAt < dateTo);
            }

            if (!_authService.IsAdmin())
            {
                var token = _tokenService.GetToken();
                query = query.Where(x => x.Id == token.UserId);
            }

            if (rq.Roles.Count > 0)
                query = query.Where(x => rq.Roles.Contains(x.RoleId));

            return query;
        }

        private static IQueryable<User> OrderQuery(IQueryable<User> query, SortRequest rq)
        {
            return rq.ColumnSort switch
            {
                "createdAt" => rq.SortDirection == SortDirectionCode.ASC ? query.OrderBy(x => x.CreatedAt) : query.OrderByDescending(x => x.CreatedAt),
                _ => query.OrderBy(x => x.Email),
            };
        }
        #endregion
    }
}
