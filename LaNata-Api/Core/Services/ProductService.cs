using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LaNata_Api.Audit.Helpers;
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
using LaNata_Api.Core.Models.Internal;
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
        #region Constructor
        private readonly APIContext _db = context;
        private readonly AuditService _auditService = auditService;
        private readonly ExportService _exportService = exportService;
        private readonly ReportService _reportService = reportService;
        private readonly TokenService _tokenService = tokenService;
        #endregion

        #region Constants
        private const string NoCategory = "Sin categoría";
        private const int RepricedOrdersInPayload = 200;
        private const string VariantsField = "Variants";
        private const string VariantsFieldName = "Variantes";
        private const string NoPriceExtra = "Sin recargo";
        #endregion

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
                ?? ServiceRequestValidator.ValidateSearch(rq.Search)
                ?? ServiceRequestValidator.ValidatePositiveId(rq.CategoryId, "categoría");
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
                            CategoryId = x.CategoryId,
                            CategoryDescription = x.Category.Description,
                            VariantsCount = x.Variants.Count,
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
                    CategoryId = x.CategoryId,
                    CategoryDescription = x.Category.Description,
                    CreatedAt = x.CreatedAt,
                    Variants = x.Variants
                        .OrderBy(v => v.Description)
                        .Select(v => new GetOneResponse.VariantItem
                        {
                            Id = v.Id,
                            Description = v.Description,
                            Stock = v.Stock,
                            PriceExtra = v.PriceExtra
                        })
                        .ToList()
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
                BreakPrice = rq.BreakPrice,
                CategoryId = rq.CategoryId
            };

            if (!rs.Attach(await ValidateProduct<CreateResponse>(product)).Success)
                return rs;

            product.Variants = [.. (rq.Variants ?? []).Select(x => new ProductVariant
            {
                Description = x.Description?.Trim(),
                Stock = x.Stock,
                PriceExtra = x.PriceExtra
            })];

            var variantsResponse = ValidateVariants<CreateResponse>(product.Variants);
            if (!rs.Attach(variantsResponse).Success)
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

            var categories = await LoadCategoryNames(product.CategoryId);
            var createChanges = BuildAuditChanges(AuditChangeList.ForCreated(product), categories);
            AddVariantAudit(createChanges, null, LiveVariants(product));

            await Audit(AuditActionCode.CREATED, product, createChanges);

            rs.Message = Messages.CRUD.EntityCreated("Producto");
            rs.Data = new CreateResponse
            {
                Id = product.Id,
                Description = product.Description,
                Stock = product.Stock,
                Price = product.Price,
                BreakPrice = product.BreakPrice,
                CategoryId = product.CategoryId
            };

            return rs;
        }

        public async Task<BaseResponse> Update(UpdateRequest rq)
        {
            var rs = new BaseResponse();

            if (rq == null || rq.Id <= 0)
                return rs.SetError(Messages.Error.FieldGraterThanZero("id"));

            var product = await _db
                .Product
                .Include(x => x.Variants)
                .FirstOrDefaultAsync(x => x.Id == rq.Id);

            if (product == null)
                return rs.SetError(Messages.Error.EntityNotFound("Producto"));

            // Cloned before the variants are reconciled: the clone is the only copy of how they
            // looked, since ReconcileVariants edits the tracked ones in place
            var previous = product.Clone();
            var previousVariants = LiveVariants(previous);

            product.Description = rq.Description?.Trim();
            product.Stock = rq.Stock;
            product.Price = rq.Price;
            product.BreakPrice = rq.BreakPrice;
            product.CategoryId = rq.CategoryId;

            var validationResponse = await ValidateProduct<object>(product);
            if (validationResponse.Error != null)
                return rs.SetError(validationResponse.Error.Message);

            var variantsResponse = await ReconcileVariants(product, rq.Variants);
            if (variantsResponse.Error != null)
                return rs.SetError(variantsResponse.Error.Message);

            var variantsValidation = ValidateVariants<object>(product.Variants);
            if (variantsValidation.Error != null)
                return rs.SetError(variantsValidation.Error.Message);

            var coverageValidation = await ValidateStockCoverage(product, previous, previousVariants);
            if (coverageValidation.Error != null)
                return rs.SetError(coverageValidation.Error.Message);

            // A move of the effective line price re-prices, either direction, only the unpaid orders
            var reprice = HasEffectivePriceChange(previous, product);
            var summary = new RepriceSummary();

            await using var transaction = await _db.Database.BeginTransactionAsync();

            try
            {
                await _db.SaveChangesAsync();

                if (reprice)
                    summary = await RepriceOpenOrders(product, previous.Price, previousVariants);

                foreach (var entry in BuildPriceHistory(product, previous, previousVariants, summary))
                    _db.ProductPriceHistory.Add(entry);

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                return rs.SetError(Messages.Error.Exception());
            }

            var categories = await LoadCategoryNames(previous.CategoryId, product.CategoryId);
            var changes = BuildAuditChanges(AuditChangeList.For(previous, product), categories);

            AddVariantAudit(changes, previousVariants, LiveVariants(product));

            if (summary.Lines > 0 || summary.Skipped > 0)
                changes.Payload["Repricing"] = BuildRepricePayload(previous.Price, product.Price, summary);

            await Audit(AuditActionCode.UPDATED, product, changes);

            rs.Message = Messages.CRUD.EntityUpdated("Producto");
            return rs;
        }

        public async Task<BaseResponse> Delete(DeleteRequest rq)
        {
            var rs = new BaseResponse();

            if (rq == null || rq.Id <= 0)
                return rs.SetError(Messages.Error.FieldGraterThanZero("id"));

            // Included so the trail keeps the variants the product had when it was removed
            var product = await _db.Product.Include(x => x.Variants).FirstOrDefaultAsync(x => x.Id == rq.Id);
            if (product == null)
                return rs.SetError(Messages.Error.EntityNotFound("Producto"));

            // Retired, its lines stay pointing at it and the order can no longer be saved
            var today = DateTime.UtcNow.Date;
            if (await _db.ProductOrder.AnyAsync(x => x.ProductId == product.Id && x.Order.Date >= today))
                return rs.SetError(Messages.Error.ProductInUse());

            var deletedVariants = LiveVariants(product);

            product.DeletedAt = DateTime.UtcNow;

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (Exception)
            {
                return rs.SetError(Messages.Error.Exception());
            }

            var categories = await LoadCategoryNames(product.CategoryId);
            var deleteChanges = BuildAuditChanges(AuditChangeList.ForDeleted(product), categories);
            AddVariantAudit(deleteChanges, deletedVariants, null);

            await Audit(AuditActionCode.DELETED, product, deleteChanges);

            rs.Message = Messages.CRUD.EntityDeleted("Producto");
            return rs;
        }
        #endregion

        #region Methods
        public async Task<BaseResponse<GetPriceHistoryResponse>> GetPriceHistory(GetPriceHistoryRequest rq)
        {
            var rs = new BaseResponse<GetPriceHistoryResponse>();

            if (rq == null || rq.Id <= 0)
                return rs.SetError(Messages.Error.FieldGraterThanZero("id"));

            var items = await _db
                .ProductPriceHistory
                .AsNoTracking()
                .Where(x => x.ProductId == rq.Id)
                .OrderByDescending(x => x.ChangedAt)
                .Select(x => new GetPriceHistoryResponse.Item
                {
                    Id = x.Id,
                    VariantId = x.VariantId,
                    PreviousPrice = x.PreviousPrice,
                    NewPrice = x.NewPrice,
                    PreviousBreakPrice = x.PreviousBreakPrice,
                    NewBreakPrice = x.NewBreakPrice,
                    PreviousPriceExtra = x.PreviousPriceExtra,
                    NewPriceExtra = x.NewPriceExtra,
                    ChangedAt = x.ChangedAt,
                    ChangedByEmail = x.ChangedByUser.Email,
                    RepricedOrders = x.RepricedOrders,
                    RepricedLines = x.RepricedLines,
                    SkippedLines = x.SkippedLines
                })
                .ToListAsync();

            // Filter off: projecting x.Variant.Description would blank a variant retired since
            var variants = await LoadVariantNames(items.Select(x => x.VariantId));

            foreach (var item in items.Where(x => x.VariantId.HasValue))
                item.VariantDescription = variants.GetValueOrDefault(item.VariantId.Value);

            rs.Data = new GetPriceHistoryResponse { Items = items };
            return rs;
        }

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
                        CategoryDescription = x.Category.Description,
                        x.Price,
                        x.BreakPrice,
                        x.Stock,
                        Reserved = x.Orders
                            .Where(po => po.Order.Date >= dayStart
                                && po.Order.Date < dayEnd
                                && (!rq.ExcludeOrderId.HasValue || po.OrderId != rq.ExcludeOrderId.Value))
                            .Sum(po => (int?)po.Quantity) ?? 0,
                        Variants = x.Variants
                            .OrderBy(v => v.Description)
                            .Select(v => new
                            {
                                v.Id,
                                v.Description,
                                v.PriceExtra,
                                v.Stock,
                                Reserved = v.Orders
                                    .Where(po => po.Order.Date >= dayStart
                                        && po.Order.Date < dayEnd
                                        && (!rq.ExcludeOrderId.HasValue || po.OrderId != rq.ExcludeOrderId.Value))
                                    .Sum(po => (int?)po.Quantity) ?? 0
                            })
                            .ToList()
                    })
                    .Select(x => new GetStockResponse.Item
                    {
                        Id = x.Id,
                        Description = x.Description,
                        CategoryDescription = x.CategoryDescription,
                        Price = x.Price,
                        BreakPrice = x.BreakPrice,
                        Stock = x.Stock,
                        ReservedStock = x.Reserved,
                        AvailableStock = x.Stock - x.Reserved,
                        Variants = x.Variants
                            .Select(v => new GetStockResponse.Item.Variant
                            {
                                Id = v.Id,
                                Description = v.Description,
                                PriceExtra = v.PriceExtra,
                                Stock = v.Stock,
                                ReservedStock = v.Reserved,
                                AvailableStock = v.Stock - v.Reserved
                            })
                            .ToList()
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

            var categoryName = await GetCategoryName(rq);

            var columns = ExportColumns
                .For<Product>()
                .Add(x => x.Id, "Id", ExportValueFormat.Integer)
                .Add(x => x.Description, "Producto", width: 45)
                .Add("Categoría", x => x.Category == null ? NoCategory : x.Category.Description, width: 25)
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
                await AuditDocument(AuditActionCode.EXPORTED, rs.Data.FileName, rs.Data.RowCount, format.ToString(), rq, categoryName);

            return rs;
        }

        // The legacy price list handed to clients: product and price, nothing else. Filtering the
        // listing by category is what produces the partial lists (only vajilla, only mobiliario)
        public async Task<BaseResponse<ReportFile>> Report(GetAllRequest rq)
        {
            var rs = new BaseResponse<ReportFile>();

            var products = await GetProductsForDocument(rq);
            if (products.Count == 0)
                return rs.SetError(ReportMessages.NoRows());

            var categoryName = await GetCategoryName(rq);

            var columns = ReportColumns
                .For<Product>()
                .Add(x => x.Description, "Producto", width: 4)
                .AddCurrency(x => x.Price, "Precio")
                .AddCurrency(x => x.BreakPrice, "Precio por rotura", emptyText: "-")
                .Build();

            rs = _reportService.GenerateTable(new ReportTableRequest<Product>
            {
                FileName = rq?.CategoryId == null
                    ? "listado_de_productos"
                    : "listado_de_productos_categoria_" + rq.CategoryId.Value,
                Columns = columns,
                Rows = products,
                Options = new ReportOptions
                {
                    Title = CompanyInfo.Name,
                    Subtitle = CompanyInfo.Slogan + Environment.NewLine + CompanyInfo.Tagline,
                    DocumentTitle = string.IsNullOrWhiteSpace(categoryName)
                        ? "LISTADO DE PRODUCTOS"
                        : "LISTADO DE PRODUCTOS - " + categoryName.ToUpperInvariant(),
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
                    Filters = BuildReportFilters(rq, categoryName)
                }
            });

            if (rs.Success)
                await AuditDocument(AuditActionCode.REPORT_GENERATED, rs.Data.FileName, products.Count, "Pdf", rq, categoryName);

            return rs;
        }
        #endregion

        #region Audit
        private static AuditChangeSet BuildAuditChanges(AuditChangeList<Product> changeList, Dictionary<int, string> categories)
        {
            return changeList
                .AddField(x => x.Description, "Descripción")
                .AddField(x => x.Stock, "Stock")
                .AddField(x => x.Price, "Precio", AuditFormat.Currency)
                .AddField(x => x.BreakPrice, "Precio por rotura", AuditFormat.Currency)
                .AddLookup(x => x.CategoryId, value => ResolveCategoryName(value, categories), "Categoría")
                .Build();
        }

        // Variants live in their own table, so the field-by-field change list cannot see them: this
        // walks both sides by id and reports what was added, removed and edited. A removed variant is
        // still in the collection with DeletedAt set, which is why the two sides are filtered.
        private static void AddVariantAudit(AuditChangeSet changes, List<ProductVariant> previous, List<ProductVariant> updated)
        {
            if (changes.State != null)
                changes.State[VariantsField] = BuildVariantSnapshot(previous);

            if (changes.Payload != null)
                changes.Payload[VariantsField] = BuildVariantSnapshot(updated);

            // A creation has no previous version to compare against, and a deletion has no updated one
            if (previous == null || updated == null)
                return;

            foreach (var variant in updated.Where(x => previous.All(p => p.Id != x.Id)).OrderBy(x => x.Description))
                changes.Changes.Add(BuildVariantChange(null, variant));

            foreach (var variant in previous.Where(x => updated.All(u => u.Id != x.Id)).OrderBy(x => x.Description))
                changes.Changes.Add(BuildVariantChange(variant, null));

            foreach (var variant in previous.OrderBy(x => x.Description))
            {
                var current = updated.FirstOrDefault(x => x.Id == variant.Id);

                if (current != null && DescribeVariant(current) != DescribeVariant(variant))
                    changes.Changes.Add(BuildVariantChange(variant, current));
            }
        }

        private static AuditChange BuildVariantChange(ProductVariant previous, ProductVariant updated)
        {
            var previousValue = previous == null ? null : DescribeVariant(previous);
            var updatedValue = updated == null ? null : DescribeVariant(updated);
            var name = VariantName(updated ?? previous);

            var description = previous == null
                ? AuditMessages.ItemAdded(VariantsFieldName, updatedValue)
                : updated == null
                    ? AuditMessages.ItemRemoved(VariantsFieldName, previousValue)
                    : AuditMessages.FieldUpdated("la variante «" + name + "»", previousValue, updatedValue);

            return new AuditChange
            {
                Field = VariantsField,
                FieldName = previous != null && updated != null ? VariantsFieldName + " · " + name : VariantsFieldName,
                PreviousValue = previousValue,
                UpdatedValue = updatedValue,
                Description = description
            };
        }

        private static List<Dictionary<string, object>> BuildVariantSnapshot(List<ProductVariant> variants)
        {
            return [.. (variants ?? [])
                .OrderBy(x => x.Description)
                .Select(x => new Dictionary<string, object>
                {
                    ["Description"] = VariantName(x),
                    ["Stock"] = x.Stock,
                    ["PriceExtra"] = DescribePriceExtra(x)
                })];
        }

        // One line per variant instead of one per field: the change list stays short and each entry
        // still shows the whole variant before and after
        private static string DescribeVariant(ProductVariant variant)
        {
            return VariantName(variant) + " · Stock: " + variant.Stock + " · Recargo: " + DescribePriceExtra(variant);
        }

        // Same formatter the product price goes through, so both read alike inside one entry
        private static string DescribePriceExtra(ProductVariant variant)
        {
            return variant.PriceExtra.HasValue
                ? AuditFormatter.Format(variant.PriceExtra.Value, AuditFormat.Currency)
                : NoPriceExtra;
        }

        private static string VariantName(ProductVariant variant)
        {
            return string.IsNullOrWhiteSpace(variant?.Description) ? AuditMessages.EmptyValue() : variant.Description.Trim();
        }

        private static List<ProductVariant> LiveVariants(Product product)
        {
            return [.. (product?.Variants ?? []).Where(x => x.DeletedAt == null)];
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

        private async Task AuditDocument(string actionCode, string fileName, int rowCount, string format, GetAllRequest rq, string categoryName)
        {
            var payload = new Dictionary<string, object>
            {
                ["Archivo"] = fileName,
                ["Formato"] = format,
                ["Registros"] = rowCount
            };

            foreach (var filter in BuildReportFilters(rq, categoryName))
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

        private static Dictionary<string, object> BuildRepricePayload(decimal previousPrice, decimal newPrice, RepriceSummary result)
        {
            var payload = new Dictionary<string, object>
            {
                ["PrecioAnterior"] = previousPrice,
                ["PrecioNuevo"] = newPrice,
                ["PedidosActualizados"] = result.Orders,
                ["RenglonesActualizados"] = result.Lines,
                ["RenglonesOmitidos"] = result.Skipped,
                ["Pedidos"] = result.OrderIds.Take(RepricedOrdersInPayload).ToList()
            };

            if (result.OrderIds.Count > RepricedOrdersInPayload)
                payload["Truncado"] = true;

            return payload;
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

            if (product.Stock < 0)
                return rs.SetError(Messages.Error.InvalidField("stock"));

            if (product.Stock > ValidationLimits.ProductStockMax)
                return rs.SetError(Messages.Error.FieldMax("stock", ValidationLimits.ProductStockMax));

            if (product.Price <= 0)
                return rs.SetError(Messages.Error.FieldGraterThanZero("precio"));

            if (product.Price > ValidationLimits.ProductPriceMax)
                return rs.SetError(Messages.Error.FieldMax("precio", ValidationLimits.ProductPriceMax));

            if (product.BreakPrice.HasValue && product.BreakPrice <= 0)
                return rs.SetError(Messages.Error.FieldGraterThanZero("precio por rotura"));

            if (product.BreakPrice > ValidationLimits.ProductPriceMax)
                return rs.SetError(Messages.Error.FieldMax("precio por rotura", ValidationLimits.ProductPriceMax));

            if (product.CategoryId.HasValue)
            {
                if (product.CategoryId.Value <= 0)
                    return rs.SetError(Messages.Error.FieldGraterThanZero("categoría"));

                if (!await _db.Category.AnyAsync(x => x.Id == product.CategoryId.Value))
                    return rs.SetError(Messages.Error.EntityNotFound("Categoría", femine: true));
            }

            if (await _db.Product.AnyAsync(x => x.Description.ToLower() == product.Description.ToLower() && x.Id != product.Id))
                return rs.SetError(Messages.Error.UniqueField("producto", "descripción", femineField: true));

            return rs;
        }

        // Only reductions: one already oversold has to stay correctable upwards
        private async Task<BaseResponse> ValidateStockCoverage(Product product, Product previous, List<ProductVariant> previousVariants)
        {
            var rs = new BaseResponse();
            var today = DateTime.UtcNow.Date;

            if (product.Stock < previous.Stock)
            {
                var reserved = await _db
                    .ProductOrder
                    .Where(x => x.ProductId == product.Id && x.Order.Date >= today)
                    .GroupBy(x => x.Order.Date.Date)
                    .Select(x => new { Date = x.Key, Quantity = x.Sum(line => line.Quantity) })
                    .ToListAsync();

                var overbooked = reserved
                    .Where(x => x.Quantity > product.Stock)
                    .OrderBy(x => x.Date)
                    .FirstOrDefault();

                if (overbooked != null)
                    return rs.SetError(Messages.Error.ProductStockBelowReserved(overbooked.Date, overbooked.Quantity));
            }

            var previousStock = previousVariants.ToDictionary(x => x.Id, x => x.Stock);
            var reduced = LiveVariants(product)
                .Where(x => previousStock.TryGetValue(x.Id, out var stock) && x.Stock < stock)
                .ToDictionary(x => x.Id, x => x);

            if (reduced.Count == 0)
                return rs;

            var variantIds = reduced.Keys.ToList();

            var reservations = await _db
                .ProductOrder
                .Where(x => x.VariantId.HasValue && variantIds.Contains(x.VariantId.Value) && x.Order.Date >= today)
                .GroupBy(x => new { VariantId = x.VariantId.Value, Date = x.Order.Date.Date })
                .Select(x => new { x.Key.VariantId, x.Key.Date, Quantity = x.Sum(line => line.Quantity) })
                .ToListAsync();

            foreach (var reservation in reservations.OrderBy(x => x.Date).ThenBy(x => x.VariantId))
            {
                var variant = reduced[reservation.VariantId];

                if (reservation.Quantity > variant.Stock)
                    return rs.SetError(Messages.Error.VariantStockBelowReserved(variant.Description, reservation.Date, reservation.Quantity));
            }

            return rs;
        }

        private static BaseResponse<T> ValidateVariants<T>(List<ProductVariant> variants)
        {
            var rs = new BaseResponse<T>();

            if (variants == null || variants.Count == 0)
                return rs;

            if (variants.Count > ValidationLimits.ProductVariantsMax)
                return rs.SetError(Messages.Error.TooManyVariants(ValidationLimits.ProductVariantsMax));

            foreach (var variant in variants)
            {
                if (variant == null || string.IsNullOrWhiteSpace(variant.Description))
                    return rs.SetError(Messages.Error.FieldRequired("descripción de la variante"));

                if (variant.Description.Trim().Length > ValidationLimits.ProductVariantDescriptionMaxLength)
                    return rs.SetError(Messages.Error.FieldTooLong("descripción de la variante", ValidationLimits.ProductVariantDescriptionMaxLength));

                if (variant.Stock < 0)
                    return rs.SetError(Messages.Error.InvalidField("stock de la variante"));

                if (variant.Stock > ValidationLimits.ProductStockMax)
                    return rs.SetError(Messages.Error.FieldMax("stock de la variante", ValidationLimits.ProductStockMax));

                if (variant.PriceExtra.HasValue && variant.PriceExtra <= 0)
                    return rs.SetError(Messages.Error.FieldGraterThanZero("recargo de la variante"));

                if (variant.PriceExtra > ValidationLimits.ProductPriceMax)
                    return rs.SetError(Messages.Error.FieldMax("recargo de la variante", ValidationLimits.ProductPriceMax));
            }

            if (variants
                .Select(x => x.Description.Trim().ToLowerInvariant())
                .Distinct()
                .Count() != variants.Count)
                return rs.SetError(Messages.Error.UniqueField("variante", "descripción", femineField: true, femine: true));

            return rs;
        }
        #endregion

        #region Helpers
        private List<ProductPriceHistory> BuildPriceHistory(
            Product product,
            Product previous,
            List<ProductVariant> previousVariants,
            RepriceSummary summary)
        {
            var entries = new List<ProductPriceHistory>();
            var changedBy = _tokenService.TryGetToken()?.UserId;

            ProductPriceHistory Entry(int? variantId, decimal? previousPriceExtra, decimal? newPriceExtra, RepriceResult result)
            {
                return new ProductPriceHistory
                {
                    ProductId = product.Id,
                    VariantId = variantId,
                    PreviousPrice = previous.Price,
                    NewPrice = product.Price,
                    PreviousBreakPrice = previous.BreakPrice,
                    NewBreakPrice = product.BreakPrice,
                    PreviousPriceExtra = previousPriceExtra,
                    NewPriceExtra = newPriceExtra,
                    ChangedBy = changedBy,
                    RepricedOrders = result.Orders,
                    RepricedLines = result.Lines,
                    SkippedLines = result.Skipped
                };
            }

            var previousSurcharges = previousVariants.ToDictionary(x => x.Id, x => x.PriceExtra);
            var moved = LiveVariants(product)
                .Where(x => previousSurcharges.TryGetValue(x.Id, out var previousPriceExtra) && previousPriceExtra != x.PriceExtra)
                .ToList();

            if (product.Price != previous.Price || product.BreakPrice != previous.BreakPrice)
            {
                // The variants that kept their surcharge moved too and have no row of their own
                var ownRow = moved.Select(x => x.Id).ToHashSet();
                entries.Add(Entry(null, null, null, summary.Fold(x => !ownRow.Contains(x))));
            }

            foreach (var variant in moved)
                entries.Add(Entry(variant.Id, previousSurcharges[variant.Id], variant.PriceExtra, summary.For(variant.Id)));

            return entries;
        }

        private async Task<RepriceSummary> RepriceOpenOrders(
            Product product,
            decimal previousPrice,
            List<ProductVariant> previousVariants)
        {
            var summary = new RepriceSummary();
            var today = DateTime.UtcNow.Date;

            var previousSurcharges = previousVariants.ToDictionary(x => x.Id, x => x.PriceExtra ?? 0);
            var currentSurcharges = LiveVariants(product).ToDictionary(x => x.Id, x => x.PriceExtra ?? 0);

            bool TryGetPriceChange(int? variantId, out decimal oldPrice, out decimal newPrice)
            {
                oldPrice = previousPrice;
                newPrice = product.Price;

                if (!variantId.HasValue)
                    return newPrice != oldPrice;

                if (!previousSurcharges.TryGetValue(variantId.Value, out var previousSurcharge)
                    || !currentSurcharges.TryGetValue(variantId.Value, out var currentSurcharge))
                    return false;

                oldPrice += previousSurcharge;
                newPrice += currentSurcharge;
                return newPrice != oldPrice;
            }

            // Load every eligible line once. The exact old effective price check is done below with
            // the surcharge that belonged to that line's variant before the product update.
            var lines = await _db
                .ProductOrder
                .Where(x => x.ProductId == product.Id
                    && x.Order.PaidAt == null
                    && x.Order.Date >= today)
                .ToListAsync();

            if (lines.Count == 0)
                return summary;

            var repriced = 0;

            // Grouped by order and variant
            foreach (var group in lines.GroupBy(x => new { x.OrderId, x.VariantId }))
            {
                if (!TryGetPriceChange(group.Key.VariantId, out var oldPrice, out var targetPrice))
                    continue;

                var stale = group.FirstOrDefault(x => x.UnitPrice == oldPrice);
                if (stale == null)
                    continue;

                var result = summary.Track(group.Key.VariantId);
                var target = group.FirstOrDefault(x => x.UnitPrice == targetPrice);

                if (target != null)
                {
                    var quantity = target.Quantity + stale.Quantity;

                    // Over the cap both rows are left exactly as they are: half a merge would lose
                    // quantity with the row it deletes, and the product update must still succeed
                    if (quantity > ValidationLimits.OrderQuantityMax)
                    {
                        result.Skipped++;
                        continue;
                    }

                    target.Quantity = quantity;
                    _db.ProductOrder.Remove(stale);
                }
                else
                {
                    stale.UnitPrice = targetPrice;
                }

                result.Lines++;
                repriced++;

                if (!result.OrderIds.Contains(group.Key.OrderId))
                    result.OrderIds.Add(group.Key.OrderId);
            }

            if (repriced > 0)
                await _db.SaveChangesAsync();

            return summary;
        }

        private static bool HasEffectivePriceChange(Product previous, Product current)
        {
            if (current.Price != previous.Price)
                return true;

            var previousSurcharges = LiveVariants(previous).ToDictionary(x => x.Id, x => x.PriceExtra ?? 0);

            return LiveVariants(current).Any(x => previousSurcharges.TryGetValue(x.Id, out var previousSurcharge)
                && current.Price + (x.PriceExtra ?? 0) != previous.Price + previousSurcharge);
        }

        private async Task<BaseResponse> ReconcileVariants(Product product, List<UpdateRequest.VariantItem> requested)
        {
            var rs = new BaseResponse();
            var today = DateTime.UtcNow.Date;
            var incoming = requested ?? [];
            var keptIds = incoming.Where(x => x.Id.HasValue).Select(x => x.Id.Value).ToList();

            foreach (var variant in product.Variants.ToList())
            {
                if (keptIds.Contains(variant.Id))
                    continue;

                if (await _db.ProductOrder.AnyAsync(x => x.VariantId == variant.Id && x.Order.Date >= today))
                    return rs.SetError(Messages.Error.VariantInUse(variant.Description));

                variant.DeletedAt = DateTime.UtcNow;
            }

            foreach (var line in incoming)
            {
                if (line.Id.HasValue)
                {
                    var existing = product.Variants.FirstOrDefault(x => x.Id == line.Id.Value);
                    if (existing == null)
                        return rs.SetError(Messages.Error.EntityNotFound("Variante", femine: true));

                    existing.Description = line.Description?.Trim();
                    existing.Stock = line.Stock;
                    existing.PriceExtra = line.PriceExtra;
                    existing.UpdatedAt = DateTime.UtcNow;
                    continue;
                }

                product.Variants.Add(new ProductVariant
                {
                    ProductId = product.Id,
                    Description = line.Description?.Trim(),
                    Stock = line.Stock,
                    PriceExtra = line.PriceExtra
                });
            }

            return rs;
        }

        private async Task<List<Product>> GetProductsForDocument(GetAllRequest rq)
        {
            rq ??= new GetAllRequest();

            var query = _db.Product.AsNoTracking().AsQueryable();

            query = FilterQuery(query, rq);
            query = OrderQuery(query, rq);

            return await query.Include(x => x.Category).ToListAsync();
        }

        private async Task<string> GetCategoryName(GetAllRequest rq)
        {
            if (rq?.CategoryId == null)
                return null;

            return await _db
                .Category
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(x => x.Id == rq.CategoryId.Value)
                .Select(x => x.Description)
                .FirstOrDefaultAsync();
        }

        private static List<ReportField> BuildReportFilters(GetAllRequest rq, string categoryName)
        {
            var filters = new List<ReportField>();

            if (rq != null && !string.IsNullOrWhiteSpace(rq.Search))
                filters.Add(new ReportField("Búsqueda", rq.Search));

            if (!string.IsNullOrWhiteSpace(categoryName))
                filters.Add(new ReportField("Categoría", categoryName));

            return filters;
        }

        private async Task<Dictionary<int, string>> LoadVariantNames(IEnumerable<int?> ids)
        {
            var wanted = ids.Where(x => x.HasValue).Select(x => x.Value).Distinct().ToList();
            if (wanted.Count == 0)
                return [];

            return await _db
                .ProductVariant
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(x => wanted.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Description);
        }

        private async Task<Dictionary<int, string>> LoadCategoryNames(params int?[] ids)
        {
            var wanted = ids.Where(x => x.HasValue).Select(x => x.Value).Distinct().ToList();
            if (wanted.Count == 0)
                return [];

        // IgnoreQueryFilters so the audit trail still renders the name of a category removed afterwards
            return await _db
                .Category
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(x => wanted.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Description);
        }

        private static string ResolveCategoryName(object value, Dictionary<int, string> categories)
        {
            if (value == null)
                return NoCategory;

            var id = Convert.ToInt32(value);

            return categories.TryGetValue(id, out var description) ? description : NoCategory;
        }

        private static IQueryable<Product> FilterQuery(IQueryable<Product> query, GetAllRequest rq)
        {
            if (rq.CategoryId.HasValue)
                query = query.Where(x => x.CategoryId == rq.CategoryId.Value);

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
                "category" => rq.SortDirection == SortDirectionCode.ASC
                    ? query.OrderBy(x => x.Category.Description).ThenBy(x => x.Description)
                    : query.OrderByDescending(x => x.Category.Description).ThenBy(x => x.Description),
                "createdAt" => rq.SortDirection == SortDirectionCode.ASC ? query.OrderBy(x => x.CreatedAt) : query.OrderByDescending(x => x.CreatedAt),
                _ => query.OrderBy(x => x.Description),
            };
        }
        #endregion
    }
}
