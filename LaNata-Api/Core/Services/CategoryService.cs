using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using LaNata_Api.Audit.Helpers.ChangeList;
using LaNata_Api.Audit.Models.Constants;
using LaNata_Api.Audit.Models.Internal;
using LaNata_Api.Audit.Services;
using LaNata_Api.Core.DAL.DB;
using LaNata_Api.Core.Helpers;
using LaNata_Api.Core.Models;
using LaNata_Api.Core.Models.Constants;
using LaNata_Api.Core.Models.DTO;
using LaNata_Api.Core.Models.DTO.Category;

namespace LaNata_Api.Core.Services
{
    public class CategoryService(APIContext context, AuditService auditService)
    {
        private readonly APIContext _db = context;
        private readonly AuditService _auditService = auditService;

        #region Combos
        public async Task<BaseResponse<BaseComboResponse>> GetCombo()
        {
            return new BaseResponse<BaseComboResponse>
            {
                Data = new BaseComboResponse
                {
                    Items = await _db.Category
                        .OrderBy(x => x.Description)
                        .Select(x => new BaseComboResponse.Item
                        {
                            Id = x.Id,
                            Description = x.Description
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

            var validationError = ServiceRequestValidator.ValidateSort(rq)
                ?? ServiceRequestValidator.ValidateSearch(rq.Search);
            if (validationError != null)
                return new BaseResponse<GetAllResponse>().SetError(validationError);

            var query = _db.Category.AsQueryable();

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
                            Description = x.Description,
                            ProductsCount = x.Products.Count,
                            CreatedAt = x.CreatedAt
                        })
                        .ToListAsync()
                }
            };
        }

        public async Task<BaseResponse<GetOneResponse>> GetOne(GetOneRequest rq)
        {
            var rs = new BaseResponse<GetOneResponse>();

            if (rq == null || rq.Id <= 0)
                return rs.SetError(Messages.Error.FieldGraterThanZero("id"));

            var category = await _db
                .Category
                .Where(x => x.Id == rq.Id)
                .Select(x => new GetOneResponse
                {
                    Id = x.Id,
                    Description = x.Description,
                    ProductsCount = x.Products.Count,
                    CreatedAt = x.CreatedAt
                })
                .AsNoTracking()
                .FirstOrDefaultAsync();

            if (category == null)
                return rs.SetError(Messages.Error.EntityNotFound("Categoría", femine: true));

            rs.Data = category;
            return rs;
        }

        public async Task<BaseResponse<CreateResponse>> Create(CreateRequest rq)
        {
            var rs = new BaseResponse<CreateResponse>();

            if (rq == null)
                return rs.SetError(Messages.Error.FieldsRequired());

            var category = new Models.Category
            {
                Description = rq.Description?.Trim()
            };

            if (!rs.Attach(await ValidateCategory<CreateResponse>(category)).Success)
                return rs;

            try
            {
                _db.Category.Add(category);
                await _db.SaveChangesAsync();
            }
            catch (Exception)
            {
                return rs.SetError(Messages.Error.Exception());
            }

            await Audit(AuditActionCode.CREATED, category, BuildAuditChanges(AuditChangeList.ForCreated(category)));

            rs.Message = Messages.CRUD.EntityCreated("Categoría", femine: true);
            rs.Data = new CreateResponse
            {
                Id = category.Id,
                Description = category.Description
            };

            return rs;
        }

        public async Task<BaseResponse> Update(UpdateRequest rq)
        {
            var rs = new BaseResponse();

            if (rq == null || rq.Id <= 0)
                return rs.SetError(Messages.Error.FieldGraterThanZero("id"));

            var category = await _db.Category.FirstOrDefaultAsync(x => x.Id == rq.Id);
            if (category == null)
                return rs.SetError(Messages.Error.EntityNotFound("Categoría", femine: true));

            var previous = category.Clone();

            category.Description = rq.Description?.Trim();
            category.UpdatedAt = DateTime.UtcNow;

            var validationResponse = await ValidateCategory<object>(category);
            if (validationResponse.Error != null)
                return rs.SetError(validationResponse.Error.Message);

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (Exception)
            {
                return rs.SetError(Messages.Error.Exception());
            }

            await Audit(AuditActionCode.UPDATED, category, BuildAuditChanges(AuditChangeList.For(previous, category)));

            rs.Message = Messages.CRUD.EntityUpdated("Categoría", femine: true);
            return rs;
        }

        public async Task<BaseResponse> Delete(DeleteRequest rq)
        {
            var rs = new BaseResponse();

            if (rq == null || rq.Id <= 0)
                return rs.SetError(Messages.Error.FieldGraterThanZero("id"));

            var category = await _db.Category.FirstOrDefaultAsync(x => x.Id == rq.Id);
            if (category == null)
                return rs.SetError(Messages.Error.EntityNotFound("Categoría", femine: true));

            // Soft deleting a category that is in use would make the product listing project its name
            // as null with no message at all, because the global filter reaches it through the navigation
            if (await _db.Product.AnyAsync(x => x.CategoryId == category.Id))
                return rs.SetError(Messages.Error.CategoryInUse());

            category.DeletedAt = DateTime.UtcNow;

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (Exception)
            {
                return rs.SetError(Messages.Error.Exception());
            }

            await Audit(AuditActionCode.DELETED, category, BuildAuditChanges(AuditChangeList.ForDeleted(category)));

            rs.Message = Messages.CRUD.EntityDeleted("Categoría", femine: true);
            return rs;
        }
        #endregion

        #region Audit
        private static AuditChangeSet BuildAuditChanges(AuditChangeList<Models.Category> changeList)
        {
            return changeList
                .AddField(x => x.Description, "Descripción")
                .Build();
        }

        private async Task Audit(string actionCode, Models.Category category, AuditChangeSet changes)
        {
            await _auditService.Audit(new AuditRequest
            {
                ActionCode = actionCode,
                Entity = AuditEntityCode.CATEGORY,
                EntityKey = category.Id.ToString(),
                Name = category.Description,
                Changes = changes
            });
        }
        #endregion

        #region Validations
        private async Task<BaseResponse<T>> ValidateCategory<T>(Models.Category category)
        {
            var rs = new BaseResponse<T>();

            if (string.IsNullOrWhiteSpace(category.Description))
                return rs.SetError(Messages.Error.FieldRequired("descripción"));

            if (category.Description.Length > ValidationLimits.CategoryDescriptionMaxLength)
                return rs.SetError(Messages.Error.FieldTooLong("descripción", ValidationLimits.CategoryDescriptionMaxLength));

            if (await _db.Category.AnyAsync(x => x.Description.ToLower() == category.Description.ToLower() && x.Id != category.Id))
                return rs.SetError(Messages.Error.UniqueField("categoría", "descripción", femineField: true, femine: true));

            return rs;
        }
        #endregion

        #region Helpers
        private static IQueryable<Models.Category> FilterQuery(IQueryable<Models.Category> query, GetAllRequest rq)
        {
            if (!string.IsNullOrWhiteSpace(rq.Search))
            {
                var search = rq.Search.Trim();
                query = query.Where(x => EF.Functions.Like(x.Description, "%" + search + "%"));
            }

            return query;
        }

        private static IQueryable<Models.Category> OrderQuery(IQueryable<Models.Category> query, SortRequest rq)
        {
            return rq.ColumnSort switch
            {
                "productsCount" => rq.SortDirection == SortDirectionCode.ASC ? query.OrderBy(x => x.Products.Count) : query.OrderByDescending(x => x.Products.Count),
                "createdAt" => rq.SortDirection == SortDirectionCode.ASC ? query.OrderBy(x => x.CreatedAt) : query.OrderByDescending(x => x.CreatedAt),
                _ => query.OrderBy(x => x.Description),
            };
        }
        #endregion
    }
}
