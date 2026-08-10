using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using Template_Api.Core.DAL.DB;
using Template_Api.Core.Models;
using Template_Api.Core.Models.Constants;
using Template_Api.Core.Models.DTO;
using Template_Api.Core.Models.DTO.GenericItem;

namespace Template_Api.Core.Services
{
    public class GenericItemService(APIContext context)
    {
        private readonly APIContext _db = context;

        #region Combos
        public async Task<BaseResponse<BaseComboResponse>> GetCombo()
        {
            return new BaseResponse<BaseComboResponse>
            {
                Data = new BaseComboResponse
                {
                    Items = await _db.GenericItem
                        .Where(x => x.IsActive)
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
        #endregion

        #region CRUD
        public async Task<BaseResponse<GetAllResponse>> GetAll(GetAllRequest rq)
        {
            var query = _db.GenericItem.AsQueryable();

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
                            Name = x.Name,
                            Description = x.Description,
                            IsActive = x.IsActive,
                            CreatedAt = x.CreatedAt
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

            var item = await _db
                .GenericItem
                .Where(x => x.Id == rq.Id)
                .Select(x => new GetOneResponse
                {
                    Id = x.Id,
                    Name = x.Name,
                    Description = x.Description,
                    IsActive = x.IsActive,
                    CreatedAt = x.CreatedAt
                })
                .AsNoTracking()
                .FirstOrDefaultAsync();

            if (item == null)
                return rs.SetError(Messages.Error.EntityNotFound("Elemento"));

            rs.Data = item;
            return rs;
        }

        public async Task<BaseResponse<CreateResponse>> Create(CreateRequest rq)
        {
            var rs = new BaseResponse<CreateResponse>();

            if (string.IsNullOrWhiteSpace(rq.Name))
                return rs.SetError(Messages.Error.FieldRequired("nombre"));

            var normalizedName = rq.Name.Trim();
            if (await _db.GenericItem.AnyAsync(x => x.Name.ToLower() == normalizedName.ToLower()))
                return rs.SetError(Messages.Error.UniqueField("elemento", "nombre"));

            var item = new GenericItem
            {
                Name = normalizedName,
                Description = rq.Description,
                IsActive = rq.IsActive
            };

            try
            {
                _db.GenericItem.Add(item);
                await _db.SaveChangesAsync();
            }
            catch (Exception)
            {
                return rs.SetError(Messages.Error.Exception());
            }

            rs.Message = Messages.CRUD.EntityCreated("Elemento");
            rs.Data = new CreateResponse
            {
                Id = item.Id,
                Name = item.Name,
                Description = item.Description,
                IsActive = item.IsActive
            };

            return rs;
        }

        public async Task<BaseResponse> Update(UpdateRequest rq)
        {
            var rs = new BaseResponse();

            if (string.IsNullOrWhiteSpace(rq.Name))
                return rs.SetError(Messages.Error.FieldRequired("nombre"));

            var item = await _db.GenericItem.FirstOrDefaultAsync(x => x.Id == rq.Id);
            if (item == null)
                return rs.SetError(Messages.Error.EntityNotFound("Elemento"));

            var normalizedName = rq.Name.Trim();
            if (await _db.GenericItem.AnyAsync(x => x.Name.ToLower() == normalizedName.ToLower() && x.Id != rq.Id))
                return rs.SetError(Messages.Error.UniqueField("elemento", "nombre"));

            item.Name = normalizedName;
            item.Description = rq.Description;
            item.IsActive = rq.IsActive;
            item.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (Exception)
            {
                return rs.SetError(Messages.Error.Exception());
            }

            rs.Message = Messages.CRUD.EntityUpdated("Elemento");
            return rs;
        }

        public async Task<BaseResponse> Delete(DeleteRequest rq)
        {
            var rs = new BaseResponse();

            var item = await _db.GenericItem.FirstOrDefaultAsync(x => x.Id == rq.Id);
            if (item == null)
                return rs.SetError(Messages.Error.EntityNotFound("Elemento"));

            item.DeletedAt = DateTime.UtcNow;

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (Exception)
            {
                return rs.SetError(Messages.Error.Exception());
            }

            rs.Message = Messages.CRUD.EntityDeleted("Elemento");
            return rs;
        }

        public async Task<BaseResponse> Activate(ActivateRequest rq)
        {
            var rs = new BaseResponse();

            var item = await _db.GenericItem.FirstOrDefaultAsync(x => x.Id == rq.Id);
            if (item == null)
                return rs.SetError(Messages.Error.EntityNotFound("Elemento"));

            item.IsActive = true;
            item.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (Exception)
            {
                return rs.SetError(Messages.Error.Exception());
            }

            rs.Message = Messages.CRUD.EntityActivated("Elemento");
            return rs;
        }
        #endregion

        #region Helpers
        private static IQueryable<GenericItem> FilterQuery(IQueryable<GenericItem> query, GetAllRequest rq)
        {
            if (!string.IsNullOrWhiteSpace(rq.Search))
            {
                var search = rq.Search.Trim().ToLower();
                query = query.Where(x => x.Name.ToLower().Contains(search) || (x.Description != null && x.Description.ToLower().Contains(search)));
            }

            if (rq.IsActive.HasValue)
                query = query.Where(x => x.IsActive == rq.IsActive.Value);

            return query;
        }

        private static IQueryable<GenericItem> OrderQuery(IQueryable<GenericItem> query, PaginateRequest rq)
        {
            return rq.ColumnSort switch
            {
                "name" => rq.SortDirection == SortDirectionCode.ASC ? query.OrderBy(x => x.Name) : query.OrderByDescending(x => x.Name),
                "createdAt" => rq.SortDirection == SortDirectionCode.ASC ? query.OrderBy(x => x.CreatedAt) : query.OrderByDescending(x => x.CreatedAt),
                _ => query.OrderBy(x => x.Name),
            };
        }
        #endregion
    }
}
