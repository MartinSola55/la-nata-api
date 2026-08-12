using LaNata_Api.Audit.Models;
using LaNata_Api.Audit.Models.Constants;
using LaNata_Api.Audit.Models.DTO;
using LaNata_Api.Audit.Models.Internal;
using LaNata_Api.Core.DAL.DB;
using LaNata_Api.Core.Helpers;
using LaNata_Api.Core.Models.Constants;
using LaNata_Api.Core.Models.DTO;
using LaNata_Api.Core.Models.Internal;
using LaNata_Api.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace LaNata_Api.Audit.Services
{
    public class AuditService(APIContext context, DbContextOptions<APIContext> contextOptions, TokenService tokenService, IHttpContextAccessor httpContextAccessor)
    {
        private const int ACTION_CODE_MAX_LENGTH = 100;
        private const int ENTITY_MAX_LENGTH = 100;
        private const int ENTITY_KEY_MAX_LENGTH = 100;
        private const int NAME_MAX_LENGTH = 200;
        private const int DESCRIPTION_MAX_LENGTH = 1000;
        private const int USER_EMAIL_MAX_LENGTH = 256;
        private const int IP_ADDRESS_MAX_LENGTH = 64;

        private readonly APIContext _db = context;
        private readonly DbContextOptions<APIContext> _contextOptions = contextOptions;
        private readonly Token _token = tokenService.TryGetToken();
        private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

        #region Methods
        public async Task<BaseResponse> Audit(AuditRequest rq)
        {
            var rs = new BaseResponse();

            if (rq == null || string.IsNullOrWhiteSpace(rq.ActionCode))
                return rs.SetError(AuditMessages.ActionRequired());

            if (string.IsNullOrWhiteSpace(rq.Entity))
                return rs.SetError(AuditMessages.EntityRequired());

            if (rq.ActionCode.Length > ACTION_CODE_MAX_LENGTH)
                return rs.SetError(Messages.Error.FieldTooLong("acción", ACTION_CODE_MAX_LENGTH));

            if (rq.Entity.Length > ENTITY_MAX_LENGTH)
                return rs.SetError(Messages.Error.FieldTooLong("entidad", ENTITY_MAX_LENGTH));

            try
            {
                if (!await _db.AuditAction.AnyAsync(x => x.ActionCode == rq.ActionCode))
                    return rs.SetError(AuditMessages.UnknownAction(rq.ActionCode));

                var entry = new AuditEntry
                {
                    Date = rq.Date ?? DateTime.UtcNow,
                    ActionCode = rq.ActionCode,
                    Entity = rq.Entity,
                    EntityKey = Truncate(rq.EntityKey, ENTITY_KEY_MAX_LENGTH),
                    Name = Truncate(rq.Name, NAME_MAX_LENGTH),
                    Description = Truncate(rq.Description, DESCRIPTION_MAX_LENGTH),
                    UserId = rq.UserId ?? _token?.UserId,
                    UserName = Truncate(rq.UserName ?? _token?.Email, NAME_MAX_LENGTH),
                    UserEmail = Truncate(rq.UserEmail ?? _token?.Email, USER_EMAIL_MAX_LENGTH),
                    IpAddress = Truncate(rq.IpAddress ?? _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString(), IP_ADDRESS_MAX_LENGTH),
                    State = (rq.State ?? rq.Changes?.State).Serialize(),
                    Payload = (rq.Payload ?? rq.Changes?.Payload).Serialize(),
                    Summary = rq.Changes?.Changes.Serialize()
                };

                // A dedicated context keeps the audit write independent: calling Audit() never
                // flushes whatever the calling service still has pending on its own context
                using var db = new APIContext(_contextOptions);
                db.AuditEntry.Add(entry);
                await db.SaveChangesAsync();
            }
            catch (Exception)
            {
                return rs.SetError(AuditMessages.AuditException());
            }

            return rs;
        }
        #endregion

        #region CRUD
        public async Task<BaseResponse<GetAllResponse>> GetAll(GetAllRequest rq)
        {
            rq ??= new GetAllRequest();
            var sr = new BaseResponse<GetAllResponse>();

            var validationError = ValidateGetAllRequest(rq);
            if (validationError != null)
                return sr.SetError(validationError);

            var query = _db
                .AuditEntry
                .AsNoTracking()
                .Include(x => x.Action)
                .ThenInclude(x => x.Category)
                .AsQueryable();

            query = FilterQuery(query, rq);
            query = OrderQuery(query, rq);

            sr.Data = new GetAllResponse
            {
                Items = await query
                    .Select(x => new GetAllResponse.Item
                    {
                        Id = x.Id,
                        Date = x.Date,
                        ActionCode = x.ActionCode,
                        ActionName = x.Action.ActionName,
                        CategoryCode = x.Action.CategoryCode,
                        CategoryName = x.Action.Category.CategoryName,
                        Entity = x.Entity,
                        EntityKey = x.EntityKey,
                        Name = x.Name,
                        Description = x.Description,
                        UserId = x.UserId,
                        UserName = x.UserName,
                        UserEmail = x.UserEmail
                    })
                    .Skip((rq.Page - 1) * Pagination.DefaultPageSize)
                    .Take(Pagination.DefaultPageSize)
                    .ToListAsync()
            };

            return sr;
        }

        public async Task<BaseResponse<GetOneResponse>> GetOne(GetOneRequest rq)
        {
            var rs = new BaseResponse<GetOneResponse>();

            if (rq == null || rq.Id <= 0)
                return rs.SetError(Messages.Error.FieldGraterThanZero("id"));

            var entry = await _db
                .AuditEntry
                .AsNoTracking()
                .Include(x => x.Action)
                .ThenInclude(x => x.Category)
                .FirstOrDefaultAsync(x => x.Id == rq.Id);

            if (entry == null)
                return rs.SetError(AuditMessages.EntryNotFound());

            rs.Data = new GetOneResponse
            {
                Id = entry.Id,
                Date = entry.Date,
                ActionCode = entry.ActionCode,
                ActionName = entry.Action?.ActionName,
                CategoryCode = entry.Action?.CategoryCode,
                CategoryName = entry.Action?.Category?.CategoryName,
                Entity = entry.Entity,
                EntityKey = entry.EntityKey,
                Name = entry.Name,
                Description = entry.Description,
                UserId = entry.UserId,
                UserName = entry.UserName,
                UserEmail = entry.UserEmail,
                IpAddress = entry.IpAddress,
                State = DeserializeNode(entry.State),
                Payload = DeserializeNode(entry.Payload),
                Summary = entry.Summary.Deserialize<List<AuditChange>>()
            };

            return rs;
        }

        public async Task<BaseResponse<GetFiltersResponse>> GetFilters()
        {
            return new BaseResponse<GetFiltersResponse>
            {
                Data = new GetFiltersResponse
                {
                    Categories = await _db
                        .AuditCategory
                        .AsNoTracking()
                        .OrderBy(x => x.CategoryName)
                        .Select(x => new GetFiltersResponse.CategoryItem
                        {
                            CategoryCode = x.CategoryCode,
                            CategoryName = x.CategoryName
                        })
                        .ToListAsync(),
                    Actions = await _db
                        .AuditAction
                        .AsNoTracking()
                        .OrderBy(x => x.ActionName)
                        .Select(x => new GetFiltersResponse.ActionItem
                        {
                            ActionCode = x.ActionCode,
                            ActionName = x.ActionName,
                            CategoryCode = x.CategoryCode
                        })
                        .ToListAsync()
                }
            };
        }
        #endregion

        #region Private
        private static string ValidateGetAllRequest(GetAllRequest rq)
        {
            var paginationError = ServiceRequestValidator.ValidatePagination(rq);
            if (paginationError != null)
                return paginationError;

            if (rq.DateFrom.HasValue && rq.DateTo.HasValue && rq.DateFrom > rq.DateTo)
                return Messages.Error.InvalidDateRange();

            if (!string.IsNullOrEmpty(rq.Entity) && rq.Entity.Length > ENTITY_MAX_LENGTH)
                return Messages.Error.FieldTooLong("entidad", ENTITY_MAX_LENGTH);

            if (!string.IsNullOrEmpty(rq.EntityKey) && rq.EntityKey.Length > ENTITY_KEY_MAX_LENGTH)
                return Messages.Error.FieldTooLong("id de entidad", ENTITY_KEY_MAX_LENGTH);

            if (rq.UserId.HasValue && rq.UserId.Value <= 0)
                return Messages.Error.FieldGraterThanZero("usuario");

            return ServiceRequestValidator.ValidateSearch(rq.Search);
        }

        private static IQueryable<AuditEntry> FilterQuery(IQueryable<AuditEntry> query, GetAllRequest rq)
        {
            if (rq.DateFrom.HasValue)
                query = query.Where(x => x.Date >= rq.DateFrom.Value);

            if (rq.DateTo.HasValue)
                query = query.Where(x => x.Date < rq.DateTo.Value.Date.AddDays(1));

            if (rq.ActionCodes != null && rq.ActionCodes.Count > 0)
                query = query.Where(x => rq.ActionCodes.Contains(x.ActionCode));

            if (rq.CategoryCodes != null && rq.CategoryCodes.Count > 0)
                query = query.Where(x => rq.CategoryCodes.Contains(x.Action.CategoryCode));

            if (!string.IsNullOrWhiteSpace(rq.Entity))
                query = query.Where(x => x.Entity == rq.Entity);

            if (!string.IsNullOrWhiteSpace(rq.EntityKey))
                query = query.Where(x => x.EntityKey == rq.EntityKey);

            if (rq.UserId.HasValue)
                query = query.Where(x => x.UserId == rq.UserId.Value);

            if (!rq.IncludeInternal)
                query = query.Where(x => !x.Action.IsInternal && !x.Action.Category.IsInternal);

            if (!string.IsNullOrWhiteSpace(rq.Search))
            {
                var search = rq.Search.Trim();

                query = query.Where(x =>
                    EF.Functions.Like(x.Name, "%" + search + "%")
                    || EF.Functions.Like(x.Description, "%" + search + "%")
                    || EF.Functions.Like(x.UserName, "%" + search + "%")
                    || EF.Functions.Like(x.Action.ActionName, "%" + search + "%"));
            }

            return query;
        }

        private static IQueryable<AuditEntry> OrderQuery(IQueryable<AuditEntry> query, GetAllRequest rq)
        {
            return rq.SortDirection == SortDirectionCode.ASC
                ? query.OrderBy(x => x.Date)
                : query.OrderByDescending(x => x.Date);
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
                return value;

            return value[..maxLength];
        }

        private static JsonNode DeserializeNode(string json)
        {
            return string.IsNullOrWhiteSpace(json) ? null : JsonNode.Parse(json);
        }
        #endregion
    }
}
