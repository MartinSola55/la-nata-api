using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
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
using LaNata_Api.Core.Models.DTO.Product;
using LaNata_Api.Export.Helpers;
using LaNata_Api.Export.Models.Constants;
using LaNata_Api.Export.Models.Internal;
using LaNata_Api.Export.Services;
using LaNata_Api.Report.Helpers;
using LaNata_Api.Report.Models.Constants;
using LaNata_Api.Report.Models.Internal;
using LaNata_Api.Report.Services;

namespace LaNata_Api.Core.Services
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

            var query = _db.Product.AsQueryable();

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
                            Stock = x.Stock,
                            Price = x.Price,
                            BreakPrice = x.BreakPrice,
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

            var product = await _db
                .Product
                .Where(x => x.Id == rq.Id)
                .Select(x => new GetOneResponse
                {
                    Id = x.Id,
                    Description = x.Description,
                    Stock = x.Stock,
                    Price = x.Price,
                    BreakPrice = x.BreakPrice,
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
                Description = rq.Description?.Trim(),
                Stock = rq.Stock,
                Price = rq.Price,
                BreakPrice = rq.BreakPrice
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
                Description = product.Description,
                Stock = product.Stock,
                Price = product.Price,
                BreakPrice = product.BreakPrice
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

            product.Description = rq.Description?.Trim();
            product.Stock = rq.Stock;
            product.Price = rq.Price;
            product.BreakPrice = rq.BreakPrice;
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
        // Availability for an event date: catalogue stock minus everything the orders of that day
        // already reserve. ExcludeOrderId leaves the order being edited out of the count, so its own
        // reservation does not look like someone else's.
        public async Task<BaseResponse<GetStockResponse>> GetStock(GetStockRequest rq)
        {
            var rs = new BaseResponse<GetStockResponse>();

            if (rq == null || rq.Date == default)
                return rs.SetError(Messages.Error.FieldRequired("fecha"));

            var validationError = ServiceRequestValidator.ValidateSearch(rq.Search)
                ?? ServiceRequestValidator.ValidatePositiveId(rq.ExcludeOrderId, "pedido");
            if (validationError != null)
                return rs.SetError(validationError);

            // Stored event dates carry a time component, so availability is counted over a day range
            var dayStart = rq.Date.Date;
            var dayEnd = dayStart.AddDays(1);

            var query = _db.Product.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(rq.Search))
            {
                var search = rq.Search.Trim();
                query = query.Where(x => EF.Functions.Like(x.Description, "%" + search + "%"));
            }

            rs.Data = new GetStockResponse
            {
                Items = await query
                    .OrderBy(x => x.Description)
                    .Select(x => new
                    {
                        x.Id,
                        x.Description,
                        x.Price,
                        x.BreakPrice,
                        x.Stock,
                        Reserved = x.Orders
                            .Where(po => po.Order.Date >= dayStart
                                && po.Order.Date < dayEnd
                                && (!rq.ExcludeOrderId.HasValue || po.OrderId != rq.ExcludeOrderId.Value))
                            .Sum(po => (int?)po.Quantity) ?? 0
                    })
                    .Select(x => new GetStockResponse.Item
                    {
                        Id = x.Id,
                        Description = x.Description,
                        Price = x.Price,
                        BreakPrice = x.BreakPrice,
                        Stock = x.Stock,
                        ReservedStock = x.Reserved,
                        AvailableStock = x.Stock - x.Reserved
                    })
                    .ToListAsync()
            };

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
                .Add(x => x.Description, "Producto", width: 45)
                .Add(x => x.Stock, "Stock", ExportValueFormat.Integer)
                .Add(x => x.Price, "Precio", ExportValueFormat.Currency)
                .Add(x => x.BreakPrice, "Precio por rotura", ExportValueFormat.Currency)
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

        // The legacy price list handed to clients: product and price, nothing else
        public async Task<BaseResponse<ReportFile>> Report(GetAllRequest rq)
        {
            var rs = new BaseResponse<ReportFile>();

            var products = await GetProductsForDocument(rq);
            if (products.Count == 0)
                return rs.SetError(ReportMessages.NoRows());

            var columns = ReportColumns
                .For<Product>()
                .Add(x => x.Description, "Producto", width: 4)
                .AddCurrency(x => x.Price, "Precio")
                .AddCurrency(x => x.BreakPrice, "Precio por rotura")
                .Build();

            rs = _reportService.GenerateTable(new ReportTableRequest<Product>
            {
                FileName = "listado_de_productos",
                Columns = columns,
                Rows = products,
                Options = new ReportOptions
                {
                    Title = CompanyInfo.Name,
                    Subtitle = CompanyInfo.Slogan + Environment.NewLine + CompanyInfo.Tagline,
                    DocumentTitle = "LISTADO DE PRODUCTOS",
                    Logo = ReportAssets.Read("logo.png"),
                    ContactLines =
                    [
                        "Fecha: " + DateTime.Now.ToString("dd/MM/yyyy"),
                        "CUIT: " + CompanyInfo.Cuit,
                        CompanyInfo.Street + " - Tel: " + CompanyInfo.Phone,
                        CompanyInfo.City
                    ],
                    Author = _tokenService.TryGetToken()?.Email,
                    FooterNote = CompanyInfo.FooterNote,
                    Filters = BuildReportFilters(rq)
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
                .AddField(x => x.Description, "Descripción")
                .AddField(x => x.Stock, "Stock")
                .AddField(x => x.Price, "Precio", AuditFormat.Currency)
                .AddField(x => x.BreakPrice, "Precio por rotura", AuditFormat.Currency)
                .Build();
        }

        private async Task Audit(string actionCode, Product product, AuditChangeSet changes)
        {
            await _auditService.Audit(new AuditRequest
            {
                ActionCode = actionCode,
                Entity = AuditEntityCode.PRODUCT,
                EntityKey = product.Id.ToString(),
                Name = product.Description,
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

            if (string.IsNullOrWhiteSpace(product.Description))
                return rs.SetError(Messages.Error.FieldRequired("descripción"));

            if (product.Description.Length > ValidationLimits.ProductDescriptionMaxLength)
                return rs.SetError(Messages.Error.FieldTooLong("descripción", ValidationLimits.ProductDescriptionMaxLength));

            if (product.Stock < 0 || product.Stock > ValidationLimits.ProductStockMax)
                return rs.SetError(Messages.Error.InvalidField("stock"));

            if (product.Price <= 0 || product.Price > ValidationLimits.ProductPriceMax)
                return rs.SetError(Messages.Error.FieldGraterThanZero("precio"));

            if (product.BreakPrice.HasValue && (product.BreakPrice <= 0 || product.BreakPrice > ValidationLimits.ProductPriceMax))
                return rs.SetError(Messages.Error.FieldGraterThanZero("precio por rotura"));

            if (await _db.Product.AnyAsync(x => x.Description.ToLower() == product.Description.ToLower() && x.Id != product.Id))
                return rs.SetError(Messages.Error.UniqueField("producto", "descripción", femineField: true));

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

            if (rq != null && !string.IsNullOrWhiteSpace(rq.Search))
                filters.Add(new ReportField("Búsqueda", rq.Search));

            return filters;
        }

        private static IQueryable<Product> FilterQuery(IQueryable<Product> query, GetAllRequest rq)
        {
            if (!string.IsNullOrWhiteSpace(rq.Search))
            {
                var search = rq.Search.Trim();
                query = query.Where(x => EF.Functions.Like(x.Description, "%" + search + "%"));
            }

            return query;
        }

        private static IQueryable<Product> OrderQuery(IQueryable<Product> query, SortRequest rq)
        {
            return rq.ColumnSort switch
            {
                "stock" => rq.SortDirection == SortDirectionCode.ASC ? query.OrderBy(x => x.Stock) : query.OrderByDescending(x => x.Stock),
                "price" => rq.SortDirection == SortDirectionCode.ASC ? query.OrderBy(x => x.Price) : query.OrderByDescending(x => x.Price),
                "createdAt" => rq.SortDirection == SortDirectionCode.ASC ? query.OrderBy(x => x.CreatedAt) : query.OrderByDescending(x => x.CreatedAt),
                _ => query.OrderBy(x => x.Description),
            };
        }
        #endregion
    }
}
