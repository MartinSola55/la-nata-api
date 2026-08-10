using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Template_Api.Audit.Helpers.ChangeList;
using Template_Api.Audit.Models.Constants;
using Template_Api.Audit.Models.Internal;
using Template_Api.Audit.Services;
using Template_Api.Core.DAL.DB;
using Template_Api.Core.Helpers;
using Template_Api.Core.Models;
using Template_Api.Core.Models.Constants;
using Template_Api.Core.Models.DTO;
using Template_Api.Core.Models.DTO.Product;
using Template_Api.Export.Helpers;
using Template_Api.Export.Models.Constants;
using Template_Api.Export.Models.Internal;
using Template_Api.Export.Services;
using Template_Api.Report.Helpers;
using Template_Api.Report.Models.Constants;
using Template_Api.Report.Models.Internal;
using Template_Api.Report.Services;

namespace Template_Api.Core.Services
{
    public class ProductService(APIContext context, AuditService auditService, ExportService exportService, ReportService reportService, TokenService tokenService)
    {
        private readonly APIContext _db = context;
        private readonly AuditService _auditService = auditService;
        private readonly ExportService _exportService = exportService;
        private readonly ReportService _reportService = reportService;
        private readonly TokenService _tokenService = tokenService;

        #region Combos
        public async Task<BaseResponse<BaseComboResponse>> GetCombo()
        {
            return new BaseResponse<BaseComboResponse>
            {
                Data = new BaseComboResponse
                {
                    Items = await _db.Product
                        .Where(x => x.IsActive && x.Stock > 0)
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
            var rs = new BaseResponse<GetAllResponse>();

            rq ??= new GetAllRequest();

            if (rq.Page <= 0)
                return rs.SetError(Messages.Error.FieldGraterThanZero("página"));

            var query = _db.Product.AsQueryable();

            query = FilterQuery(query, rq);
            query = OrderQuery(query, rq);

            rs.Data = new GetAllResponse
            {
                Items = await query
                    .Select(x => new GetAllResponse.Item
                    {
                        Id = x.Id,
                        Name = x.Name,
                        Description = x.Description,
                        Price = x.Price,
                        Stock = x.Stock,
                        PictureUrl = x.PictureUrl,
                        CategoryId = x.CategoryId,
                        IsActive = x.IsActive,
                        CreatedAt = x.CreatedAt
                    })
                    .Skip((rq.Page - 1) * Pagination.DefaultPageSize)
                    .Take(Pagination.DefaultPageSize)
                    .ToListAsync()
            };

            return rs;
        }

        public async Task<BaseResponse<GetOneResponse>> GetOne(GetOneRequest rq)
        {
            var rs = new BaseResponse<GetOneResponse>();

            if (rq == null || rq.Id <= 0)
                return rs.SetError(Messages.Error.FieldGraterThanZero("id"));

            var product = await _db
                .Product
                .Where(x => x.Id == rq.Id)
                .Select(x => new GetOneResponse
                {
                    Id = x.Id,
                    Name = x.Name,
                    Description = x.Description,
                    Price = x.Price,
                    Stock = x.Stock,
                    PictureUrl = x.PictureUrl,
                    CategoryId = x.CategoryId,
                    IsActive = x.IsActive,
                    CreatedAt = x.CreatedAt
                })
                .AsNoTracking()
                .FirstOrDefaultAsync();

            if (product == null)
                return rs.SetError(Messages.Error.EntityNotFound("Producto"));

            rs.Data = product;
            return rs;
        }

        public async Task<BaseResponse<CreateResponse>> Create(CreateRequest rq)
        {
            var rs = new BaseResponse<CreateResponse>();

            if (rq == null)
                return rs.SetError(Messages.Error.FieldsRequired());

            var product = new Product
            {
                Name = NormalizeText(rq.Name),
                Description = NormalizeText(rq.Description),
                Price = rq.Price,
                Stock = rq.Stock,
                PictureUrl = NormalizeText(rq.PictureUrl),
                CategoryId = NormalizeText(rq.CategoryId),
                IsActive = rq.IsActive
            };

            if (!rs.Attach(await ValidateProduct<CreateResponse>(product)).Success)
                return rs;

            try
            {
                _db.Product.Add(product);
                await _db.SaveChangesAsync();
            }
            catch (Exception)
            {
                return rs.SetError(Messages.Error.Exception());
            }

            await Audit(AuditActionCode.CREATED, product, BuildAuditChanges(AuditChangeList.ForCreated(product)));

            rs.Message = Messages.CRUD.EntityCreated("Producto");
            rs.Data = new CreateResponse
            {
                Id = product.Id,
                Name = product.Name,
                Description = product.Description,
                Price = product.Price,
                Stock = product.Stock,
                PictureUrl = product.PictureUrl,
                CategoryId = product.CategoryId,
                IsActive = product.IsActive
            };

            return rs;
        }

        public async Task<BaseResponse> Update(UpdateRequest rq)
        {
            var rs = new BaseResponse();

            if (rq == null || rq.Id <= 0)
                return rs.SetError(Messages.Error.FieldGraterThanZero("id"));

            var product = await _db.Product.FirstOrDefaultAsync(x => x.Id == rq.Id);
            if (product == null)
                return rs.SetError(Messages.Error.EntityNotFound("Producto"));

            var previous = product.Clone();

            product.Name = NormalizeText(rq.Name);
            product.Description = NormalizeText(rq.Description);
            product.Price = rq.Price;
            product.Stock = rq.Stock;
            product.PictureUrl = NormalizeText(rq.PictureUrl);
            product.CategoryId = NormalizeText(rq.CategoryId);
            product.IsActive = rq.IsActive;
            product.UpdatedAt = DateTime.UtcNow;

            var validationResponse = await ValidateProduct<object>(product);
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

            await Audit(AuditActionCode.UPDATED, product, BuildAuditChanges(AuditChangeList.For(previous, product)));

            rs.Message = Messages.CRUD.EntityUpdated("Producto");
            return rs;
        }

        public async Task<BaseResponse> Delete(DeleteRequest rq)
        {
            var rs = new BaseResponse();

            if (rq == null || rq.Id <= 0)
                return rs.SetError(Messages.Error.FieldGraterThanZero("id"));

            var product = await _db.Product.FirstOrDefaultAsync(x => x.Id == rq.Id);
            if (product == null)
                return rs.SetError(Messages.Error.EntityNotFound("Producto"));

            product.DeletedAt = DateTime.UtcNow;

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (Exception)
            {
                return rs.SetError(Messages.Error.Exception());
            }

            await Audit(AuditActionCode.DELETED, product, BuildAuditChanges(AuditChangeList.ForDeleted(product)));

            rs.Message = Messages.CRUD.EntityDeleted("Producto");
            return rs;
        }
        #endregion

        #region Methods
        public async Task<BaseResponse> Activate(ActivateRequest rq)
        {
            var rs = new BaseResponse();

            if (rq == null || rq.Id <= 0)
                return rs.SetError(Messages.Error.FieldGraterThanZero("id"));

            var product = await _db.Product.FirstOrDefaultAsync(x => x.Id == rq.Id);
            if (product == null)
                return rs.SetError(Messages.Error.EntityNotFound("Producto"));

            product.IsActive = true;
            product.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (Exception)
            {
                return rs.SetError(Messages.Error.Exception());
            }

            rs.Message = Messages.CRUD.EntityActivated("Producto");
            return rs;
        }
        #endregion

        #region Documents
        public async Task<BaseResponse<ExportFile>> Export(ExportFileFormat format, GetAllRequest rq)
        {
            var rs = new BaseResponse<ExportFile>();

            var products = await GetProductsForDocument(rq);
            if (products.Count == 0)
                return rs.SetError(ExportMessages.NoRows());

            var columns = ExportColumns
                .For<Product>()
                .Add(x => x.Id, "Id", ExportValueFormat.Integer)
                .Add(x => x.Name, "Nombre")
                .Add(x => x.Description, "Descripción", width: 45)
                .Add(x => x.Price, "Precio", ExportValueFormat.Currency)
                .Add(x => x.Stock, "Stock", ExportValueFormat.Integer)
                .Add(x => x.CategoryId, "Categoría")
                .Add("Estado", x => x.IsActive, ExportValueFormat.YesNo)
                .Add(x => x.CreatedAt, "Creado", ExportValueFormat.DateTime)
                .Build();

            rs = await _exportService.Export(format, new ExportRequest<Product>
            {
                FileName = "productos",
                SheetName = "Productos",
                Columns = columns,
                Rows = products
            });

            if (rs.Success)
                await AuditDocument(AuditActionCode.EXPORTED, rs.Data.FileName, rs.Data.RowCount, format.ToString(), rq);

            return rs;
        }

        public async Task<BaseResponse<ReportFile>> Report(GetAllRequest rq)
        {
            var rs = new BaseResponse<ReportFile>();

            var products = await GetProductsForDocument(rq);
            if (products.Count == 0)
                return rs.SetError(ReportMessages.NoRows());

            var columns = ReportColumns
                .For<Product>()
                .AddFixed(x => x.Id.ToString(), "Id", 30, ReportAlignment.Center)
                .Add(x => x.Name, "Producto", width: 3)
                .Add(x => x.CategoryId, "Categoría", width: 2)
                .AddCurrency(x => x.Price, "Precio")
                .AddNumber(x => x.Stock, "Stock")
                .AddYesNo(x => x.IsActive, "Activo")
                .Build();

            rs = _reportService.GenerateTable(new ReportTableRequest<Product>
            {
                FileName = "productos",
                Columns = columns,
                Rows = products,
                Options = new ReportOptions
                {
                    Title = "Listado de productos",
                    Subtitle = "Catálogo con precios y stock vigentes",
                    Author = _tokenService.TryGetToken()?.Email,
                    Filters = BuildReportFilters(rq),
                    Summary =
                    [
                        new("Productos", products.Count.ToString()),
                        new("Activos", products.Count(x => x.IsActive).ToString()),
                        new("Sin stock", products.Count(x => x.Stock <= 0).ToString())
                    ]
                }
            });

            if (rs.Success)
                await AuditDocument(AuditActionCode.REPORT_GENERATED, rs.Data.FileName, products.Count, "Pdf", rq);

            return rs;
        }
        #endregion

        #region Audit
        private static AuditChangeSet BuildAuditChanges(AuditChangeList<Product> changeList)
        {
            return changeList
                .AddField(x => x.Name, "Nombre")
                .AddField(x => x.Description, "Descripción")
                .AddField(x => x.Price, "Precio", AuditFormat.Currency)
                .AddField(x => x.Stock, "Stock")
                .AddField(x => x.PictureUrl, "Imagen")
                .AddField(x => x.CategoryId, "Categoría")
                .AddField(x => x.IsActive, "Estado", AuditFormat.ActiveInactive)
                .Build();
        }

        private async Task Audit(string actionCode, Product product, AuditChangeSet changes)
        {
            await _auditService.Audit(new AuditRequest
            {
                ActionCode = actionCode,
                Entity = AuditEntityCode.PRODUCT,
                EntityKey = product.Id.ToString(),
                Name = product.Name,
                Changes = changes
            });
        }

        private async Task AuditDocument(string actionCode, string fileName, int rowCount, string format, GetAllRequest rq)
        {
            var payload = new Dictionary<string, object>
            {
                ["Archivo"] = fileName,
                ["Formato"] = format,
                ["Registros"] = rowCount
            };

            foreach (var filter in BuildReportFilters(rq))
                payload[filter.Label] = filter.Value;

            await _auditService.Audit(new AuditRequest
            {
                ActionCode = actionCode,
                Entity = AuditEntityCode.PRODUCT,
                Name = fileName,
                Description = "Se generó " + fileName + " con " + rowCount + " producto(s)",
                Payload = payload
            });
        }
        #endregion

        #region Validations
        private async Task<BaseResponse<T>> ValidateProduct<T>(Product product)
        {
            var rs = new BaseResponse<T>();

            if (string.IsNullOrWhiteSpace(product.Name))
                return rs.SetError(Messages.Error.FieldRequired("nombre"));

            if (product.Price <= 0)
                return rs.SetError(Messages.Error.FieldGraterThanZero("precio"));

            if (product.Stock < 0)
                return rs.SetError(Messages.Error.InvalidField("stock"));

            if (!string.IsNullOrWhiteSpace(product.PictureUrl) && !ValidateUrl(product.PictureUrl))
                return rs.SetError(Messages.Error.InvalidField("imagen"));

            if (await _db.Product.AnyAsync(x => x.Name.ToLower() == product.Name.ToLower() && x.Id != product.Id))
                return rs.SetError(Messages.Error.UniqueField("producto", "nombre"));

            return rs;
        }

        #endregion

        #region Helpers
        // Same filters and ordering as the listing, without pagination: a document must cover the
        // whole filtered set, not just the page the user is looking at
        private async Task<List<Product>> GetProductsForDocument(GetAllRequest rq)
        {
            rq ??= new GetAllRequest();

            var query = _db.Product.AsNoTracking().AsQueryable();

            query = FilterQuery(query, rq);
            query = OrderQuery(query, rq);

            return await query.ToListAsync();
        }

        private static List<ReportField> BuildReportFilters(GetAllRequest rq)
        {
            var filters = new List<ReportField>();

            if (rq == null)
                return filters;

            if (!string.IsNullOrWhiteSpace(rq.Search))
                filters.Add(new ReportField("Búsqueda", rq.Search));

            if (rq.IsActive.HasValue)
                filters.Add(new ReportField("Estado", rq.IsActive.Value ? "Activos" : "Inactivos"));

            if (rq.HasStock.HasValue)
                filters.Add(new ReportField("Stock", rq.HasStock.Value ? "Con stock" : "Sin stock"));

            return filters;
        }

        private static IQueryable<Product> FilterQuery(IQueryable<Product> query, GetAllRequest rq)
        {
            if (!string.IsNullOrWhiteSpace(rq.Search))
            {
                var search = rq.Search.Trim().ToLower();
                query = query.Where(x => x.Name.ToLower().Contains(search) || (x.Description != null && x.Description.ToLower().Contains(search)));
            }

            if (rq.IsActive.HasValue)
                query = query.Where(x => x.IsActive == rq.IsActive.Value);

            if (rq.HasStock.HasValue)
                query = rq.HasStock.Value ? query.Where(x => x.Stock > 0) : query.Where(x => x.Stock <= 0);

            return query;
        }

        private static IQueryable<Product> OrderQuery(IQueryable<Product> query, PaginateRequest rq)
        {
            return rq.ColumnSort switch
            {
                "name" => rq.SortDirection == SortDirectionCode.ASC ? query.OrderBy(x => x.Name) : query.OrderByDescending(x => x.Name),
                "price" => rq.SortDirection == SortDirectionCode.ASC ? query.OrderBy(x => x.Price) : query.OrderByDescending(x => x.Price),
                "stock" => rq.SortDirection == SortDirectionCode.ASC ? query.OrderBy(x => x.Stock) : query.OrderByDescending(x => x.Stock),
                "createdAt" => rq.SortDirection == SortDirectionCode.ASC ? query.OrderBy(x => x.CreatedAt) : query.OrderByDescending(x => x.CreatedAt),
                _ => query.OrderBy(x => x.Name),
            };
        }

        private static string NormalizeText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static bool ValidateUrl(string value)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
                return false;

            return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
        }
        #endregion
    }
}
