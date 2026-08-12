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
using LaNata_Api.Core.Models.DTO.Order;
using LaNata_Api.Core.Models.Internal;
using LaNata_Api.Export.Helpers;
using LaNata_Api.Export.Models.Constants;
using LaNata_Api.Export.Models.Internal;
using LaNata_Api.Export.Services;
using LaNata_Api.Report.Helpers;
using LaNata_Api.Report.Models.Internal;
using LaNata_Api.Report.Services;

namespace LaNata_Api.Core.Services
{
    public class OrderService(APIContext context, AuditService auditService, ExportService exportService, ReportService reportService, TokenService tokenService)
    {
        private const string ShipmentRowDescription = "Flete";

        // Every one of the 2200 rows already in production stores the event day at 12:00, because the
        // legacy app added 12 hours to dodge a timezone problem in its date input. New rows keep the
        // same time so the column stays homogeneous; reads never rely on it and filter by day range.
        private const int EventDateHour = 12;

        private readonly APIContext _db = context;
        private readonly AuditService _auditService = auditService;
        private readonly ExportService _exportService = exportService;
        private readonly ReportService _reportService = reportService;
        private readonly TokenService _tokenService = tokenService;

        #region CRUD
        public async Task<BaseResponse<GetAllResponse>> GetAll(GetAllRequest rq)
        {
            rq ??= new GetAllRequest();

            var validationError = ServiceRequestValidator.ValidateSort(rq)
                ?? ServiceRequestValidator.ValidateSearch(rq.Search);
            if (validationError != null)
                return new BaseResponse<GetAllResponse>().SetError(validationError);

            var query = _db.Order.Include(x => x.Products).AsQueryable();

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
                            ClientName = x.ClientName,
                            Phone = x.Phone,
                            EventAddress = x.EventAddress,
                            Date = x.Date,
                            ShipmentPrice = x.ShipmentPrice,
                            Observation = x.Observation,
                            ProductsCount = x.Products.Count,
                            Total = x.Products.Sum(p => p.Quantity * p.UnitPrice) + (x.ShipmentPrice ?? 0),
                            CreatedAt = x.CreatedAt
                        })
                        .ToListAsync()
                }
            };
        }

        public async Task<BaseResponse<GetOneResponse>> GetOne(GetOneRequest rq)
        {
            var rs = new BaseResponse<GetOneResponse>();

            if (rq == null)
                return rs.SetError(Messages.Error.EntityNotFound("Pedido"));

            var order = await _db
                .Order
                .Include(x => x.Products)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == rq.Id);

            if (order == null)
                return rs.SetError(Messages.Error.EntityNotFound("Pedido"));

            rs.Data = MapOrder(order, await LoadLineProducts(order.Products));
            return rs;
        }

        public async Task<BaseResponse<CreateResponse>> Create(CreateRequest rq)
        {
            var rs = new BaseResponse<CreateResponse>();

            if (rq == null)
                return rs.SetError(Messages.Error.FieldsRequired());

            var order = new Order
            {
                ClientName = rq.ClientName?.Trim(),
                Phone = rq.Phone?.Trim(),
                EventAddress = rq.EventAddress?.Trim(),
                Date = NormalizeEventDate(rq.Date),
                ShipmentPrice = rq.ShipmentPrice,
                Observation = rq.Observation?.Trim()
            };

            if (!rs.Attach(ValidateOrder<CreateResponse>(order)).Success)
                return rs;

            var linesResponse = await BuildOrderProducts(rq.Products, order.Date);
            if (!linesResponse.Success)
                return rs.SetError(linesResponse.Error.Message);

            order.Products = linesResponse.Data;

            try
            {
                _db.Order.Add(order);
                await _db.SaveChangesAsync();
            }
            catch (Exception)
            {
                return rs.SetError(Messages.Error.Exception());
            }

            await Audit(AuditActionCode.CREATED, order, BuildAuditChanges(AuditChangeList.ForCreated(order)));

            rs.Message = Messages.CRUD.EntityCreated("Pedido");
            rs.Data = new CreateResponse
            {
                Id = order.Id,
                ClientName = order.ClientName,
                Date = order.Date,
                Total = CalculateTotal(order),
                Products = [.. order.Products.Select(x => MapOrderProduct(x, x.Product))]
            };

            return rs;
        }

        public async Task<BaseResponse> Update(UpdateRequest rq)
        {
            var rs = new BaseResponse();

            if (rq == null || rq.Id <= 0)
                return rs.SetError(Messages.Error.FieldGraterThanZero("id"));

            var order = await _db
                .Order
                .Include(x => x.Products)
                .FirstOrDefaultAsync(x => x.Id == rq.Id);

            if (order == null)
                return rs.SetError(Messages.Error.EntityNotFound("Pedido"));

            var previous = order.Clone();

            order.ClientName = rq.ClientName?.Trim();
            order.Phone = rq.Phone?.Trim();
            order.EventAddress = rq.EventAddress?.Trim();
            order.Date = NormalizeEventDate(rq.Date);
            order.ShipmentPrice = rq.ShipmentPrice;
            order.Observation = rq.Observation?.Trim();

            if (!rs.Attach(ValidateOrder(order)).Success)
                return rs;

            var linesResponse = await BuildOrderProducts(rq.Products, order.Date, order.Id, useRequestedUnitPrices: true);
            if (!linesResponse.Success)
                return rs.SetError(linesResponse.Error.Message);

            var auditProducts = await LoadLineProducts(previous.Products.Concat(linesResponse.Data));
            var auditChanges = BuildUpdateAuditChanges(previous, order, linesResponse.Data, auditProducts);
            var requestedLines = linesResponse.Data.ToDictionary(x => (x.ProductId, x.UnitPrice));

            foreach (var existingLine in order.Products.ToList())
            {
                if (requestedLines.Remove((existingLine.ProductId, existingLine.UnitPrice), out var requestedLine))
                {
                    existingLine.Quantity = requestedLine.Quantity;
                    continue;
                }

                _db.ProductOrder.Remove(existingLine);
            }

            foreach (var requestedLine in requestedLines.Values)
            {
                requestedLine.OrderId = order.Id;
                order.Products.Add(requestedLine);
            }

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (Exception)
            {
                return rs.SetError(Messages.Error.Exception());
            }

            await Audit(AuditActionCode.UPDATED, order, auditChanges);

            rs.Message = Messages.CRUD.EntityUpdated("Pedido");
            return rs;
        }

        public async Task<BaseResponse> Delete(DeleteRequest rq)
        {
            var rs = new BaseResponse();

            if (rq == null)
                return rs.SetError(Messages.Error.EntityNotFound("Pedido"));

            var order = await _db.Order.FirstOrDefaultAsync(x => x.Id == rq.Id);

            if (order == null)
                return rs.SetError(Messages.Error.EntityNotFound("Pedido"));

            order.DeletedAt = DateTime.UtcNow;

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (Exception)
            {
                return rs.SetError(Messages.Error.Exception());
            }

            await Audit(AuditActionCode.DELETED, order, BuildAuditChanges(AuditChangeList.ForDeleted(order)));

            rs.Message = Messages.CRUD.EntityDeleted("Pedido");
            return rs;
        }
        #endregion

        #region Methods
        public async Task<BaseResponse<GetDatesResponse>> GetDates(GetDatesRequest rq)
        {
            rq ??= new GetDatesRequest();

            var today = DateTime.UtcNow.Date;
            var dateFrom = (rq.DateFrom ?? new DateTime(today.Year, 1, 1)).Date;
            var dateTo = (rq.DateTo ?? new DateTime(today.Year + 1, 12, 31)).Date;

            var rs = new BaseResponse<GetDatesResponse>();

            if (dateFrom > dateTo)
                return rs.SetError(Messages.Error.InvalidDateRange());

            var rangeEnd = dateTo.AddDays(1);

            rs.Data = new GetDatesResponse
            {
                Items = await _db
                    .Order
                    .AsNoTracking()
                    .Where(x => x.Date >= dateFrom && x.Date < rangeEnd)
                    .GroupBy(x => x.Date.Date)
                    .OrderBy(x => x.Key)
                    .Select(x => new GetDatesResponse.Item
                    {
                        Date = x.Key,
                        OrdersCount = x.Count()
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

            var orders = await GetOrdersForDocument(rq);
            if (orders.Count == 0)
                return rs.SetError(ExportMessages.NoRows());

            var columns = ExportColumns
                .For<Order>()
                .Add(x => x.Id, "Id", ExportValueFormat.Integer)
                .Add(x => x.ClientName, "Cliente", width: 35)
                .Add(x => x.Phone, "Teléfono", ExportValueFormat.Text)
                .Add(x => x.EventAddress, "Dirección", width: 40)
                .Add(x => x.Date, "Fecha", ExportValueFormat.Date)
                .Add(x => x.Products.Count, "Productos", ExportValueFormat.Integer)
                .Add(x => x.ShipmentPrice, "Flete", ExportValueFormat.Currency)
                .Add("Total", x => CalculateTotal(x), ExportValueFormat.Currency)
                .Add(x => x.Observation, "Observación", width: 40)
                .Build();

            rs = await _exportService.Export(format, new ExportRequest<Order>
            {
                FileName = "pedidos",
                SheetName = "Pedidos",
                Columns = columns,
                Rows = orders
            });

            if (rs.Success)
                await AuditDocument(AuditActionCode.EXPORTED, rs.Data.FileName, rs.Data.RowCount, format.ToString(), BuildReportFilters(rq));

            return rs;
        }

        public async Task<BaseResponse<ReportFile>> Report(ReportRequest rq)
        {
            var rs = new BaseResponse<ReportFile>();

            if (rq == null)
                return rs.SetError(Messages.Error.EntityNotFound("Pedido"));

            var order = await _db
                .Order
                .Include(x => x.Products)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == rq.Id);

            if (order == null)
                return rs.SetError(Messages.Error.EntityNotFound("Pedido"));

            var lineProducts = await LoadLineProducts(order.Products);

            var productRows = order.Products
                .Select(x =>
                {
                    var product = lineProducts.GetValueOrDefault(x.ProductId);

                    return new OrderReportRow
                    {
                        Description = product?.Description,
                        Quantity = x.Quantity,
                        UnitPrice = x.UnitPrice,
                        BreakPrice = product?.BreakPrice,
                        Subtotal = x.Quantity * x.UnitPrice
                    };
                })
                .OrderBy(x => x.Description)
                .ToList();

            var rows = AppendShipmentRow(productRows, order.ShipmentPrice);
            var fields = new List<ReportField>
            {
                new("Cliente", order.ClientName),
                new("Fecha", order.Date.ToString("dd/MM/yyyy")),
                new("Lugar", order.EventAddress),
                new("Teléfono", order.Phone ?? "-")
            };

            if (!rq.WithPrice)
                fields.Insert(0, new ReportField("Pedido Nº", order.Id.ToString()));

            rs = GenerateDetailReport(rows, rq.WithPrice, "pedido_" + order.Id, fields);

            if (rs.Success)
            {
                await AuditDocument(AuditActionCode.REPORT_GENERATED, rs.Data.FileName, rows.Count, "Pdf",
                [
                    new("Pedido", order.Id.ToString()),
                    new("Cliente", order.ClientName),
                    new("Precios", rq.WithPrice ? "Sí" : "No")
                ]);
            }

            return rs;
        }

        public async Task<BaseResponse<ReportFile>> Budget(BudgetRequest rq)
        {
            var rs = new BaseResponse<ReportFile>();

            if (rq == null || rq.Products == null || rq.Products.Count == 0)
                return rs.SetError(Messages.Error.OrderWithoutProducts());

            var validationError = ValidateBudgetRequest(rq);
            if (validationError != null)
                return rs.SetError(validationError);

            var productIds = rq.Products.Select(x => x.ProductId).Distinct().ToList();
            var products = await _db.Product.AsNoTracking().Where(x => productIds.Contains(x.Id)).ToListAsync();

            var productRows = new List<OrderReportRow>();

            foreach (var requested in rq.Products.GroupBy(x => x.ProductId))
            {
                var product = products.FirstOrDefault(x => x.Id == requested.Key);
                if (product == null)
                    return rs.SetError(Messages.Error.EntityNotFound("Producto"));

                var quantity = requested.Sum(x => x.Quantity);
                if (quantity <= 0)
                    return rs.SetError(Messages.Error.FieldGraterThanZero("cantidad"));

                productRows.Add(new OrderReportRow
                {
                    Description = product.Description,
                    Quantity = quantity,
                    UnitPrice = product.Price,
                    BreakPrice = product.BreakPrice,
                    Subtotal = quantity * product.Price
                });
            }

            var rows = AppendShipmentRow([.. productRows.OrderBy(x => x.Description)], rq.ShipmentPrice);

            rs = GenerateDetailReport(rows, withPrice: true, "presupuesto", new List<ReportField>
            {
                new("Cliente", rq.ClientName?.Trim() ?? "-"),
                new("Fecha", rq.Date == default ? "-" : rq.Date.ToString("dd/MM/yyyy")),
                new("Lugar", rq.EventAddress?.Trim() ?? "-")
            });

            if (rs.Success)
            {
                await AuditDocument(AuditActionCode.REPORT_GENERATED, rs.Data.FileName, rows.Count, "Pdf",
                [
                    new("Presupuesto", rq.ClientName?.Trim() ?? "-")
                ]);
            }

            return rs;
        }
        #endregion

        #region Audit
        private static AuditChangeSet BuildAuditChanges(AuditChangeList<Order> changeList)
        {
            return changeList
                .AddField(x => x.ClientName, "Cliente")
                .AddField(x => x.Phone, "Teléfono")
                .AddField(x => x.EventAddress, "Dirección")
                .AddField(x => x.Date, "Fecha", AuditFormat.Date)
                .AddField(x => x.ShipmentPrice, "Flete", AuditFormat.Currency)
                .AddField(x => x.Observation, "Observación")
                .Build();
        }

        private static AuditChangeSet BuildUpdateAuditChanges(
            Order previous,
            Order updated,
            IEnumerable<ProductOrder> updatedProducts,
            IReadOnlyDictionary<int, Product> products)
        {
            var changes = BuildAuditChanges(AuditChangeList.For(previous, updated));
            var previousLines = previous.Products ?? [];
            var currentLines = updatedProducts?.ToList() ?? [];

            changes.State["Products"] = BuildProductAuditSnapshot(previousLines, products);
            changes.Payload["Products"] = BuildProductAuditSnapshot(currentLines, products);

            if (!HaveSameProductLines(previousLines, currentLines))
            {
                changes.Changes.Add(new AuditChange
                {
                    Field = nameof(Order.Products),
                    FieldName = "Productos",
                    PreviousValue = BuildProductLineSummary(previousLines),
                    UpdatedValue = BuildProductLineSummary(currentLines),
                    Description = "Se actualizaron los productos del pedido"
                });
            }

            return changes;
        }

        private static List<Dictionary<string, object>> BuildProductAuditSnapshot(
            IEnumerable<ProductOrder> lines,
            IReadOnlyDictionary<int, Product> products)
        {
            return lines
                .Select(x =>
                {
                    products.TryGetValue(x.ProductId, out var product);

                    return new
                    {
                        Line = x,
                        ProductName = product?.Description ?? $"Producto #{x.ProductId}"
                    };
                })
                .OrderBy(x => x.ProductName)
                .ThenBy(x => x.Line.ProductId)
                .ThenBy(x => x.Line.UnitPrice)
                .Select(x => new Dictionary<string, object>
                {
                    ["Product"] = $"#{x.Line.ProductId} - {x.ProductName}",
                    ["Quantity"] = x.Line.Quantity,
                    ["UnitPrice"] = Currency(x.Line.UnitPrice),
                    ["Subtotal"] = Currency(x.Line.Quantity * x.Line.UnitPrice)
                })
                .ToList();
        }

        private static bool HaveSameProductLines(IEnumerable<ProductOrder> previous, IEnumerable<ProductOrder> updated)
        {
            var previousValues = previous
                .OrderBy(x => x.ProductId)
                .ThenBy(x => x.UnitPrice)
                .Select(x => (x.ProductId, x.Quantity, x.UnitPrice));
            var updatedValues = updated
                .OrderBy(x => x.ProductId)
                .ThenBy(x => x.UnitPrice)
                .Select(x => (x.ProductId, x.Quantity, x.UnitPrice));

            return previousValues.SequenceEqual(updatedValues);
        }

        private static string BuildProductLineSummary(IEnumerable<ProductOrder> lines)
        {
            var values = lines.ToList();
            var quantity = values.Sum(x => (long)x.Quantity);

            return $"{values.Count} renglón(es), {quantity} unidad(es)";
        }

        private async Task Audit(string actionCode, Order order, AuditChangeSet changes)
        {
            await _auditService.Audit(new AuditRequest
            {
                ActionCode = actionCode,
                Entity = AuditEntityCode.ORDER,
                EntityKey = order.Id.ToString(),
                Name = order.ClientName,
                Changes = changes
            });
        }

        private async Task AuditDocument(string actionCode, string fileName, int rowCount, string format, List<ReportField> filters)
        {
            var payload = new Dictionary<string, object>
            {
                ["Archivo"] = fileName,
                ["Formato"] = format,
                ["Registros"] = rowCount
            };

            foreach (var filter in filters)
                payload[filter.Label] = filter.Value;

            await _auditService.Audit(new AuditRequest
            {
                ActionCode = actionCode,
                Entity = AuditEntityCode.ORDER,
                Name = fileName,
                Description = "Se generó " + fileName + " con " + rowCount + " registro(s)",
                Payload = payload
            });
        }
        #endregion

        #region Validations
        private static BaseResponse<T> ValidateOrder<T>(Order order)
        {
            var rs = new BaseResponse<T>();

            if (string.IsNullOrWhiteSpace(order.ClientName))
                return rs.SetError(Messages.Error.FieldRequired("cliente"));

            if (order.ClientName.Length > ValidationLimits.OrderClientNameMaxLength)
                return rs.SetError(Messages.Error.FieldTooLong("cliente", ValidationLimits.OrderClientNameMaxLength));

            if (string.IsNullOrWhiteSpace(order.EventAddress))
                return rs.SetError(Messages.Error.FieldRequired("dirección"));

            if (order.EventAddress.Length > ValidationLimits.OrderAddressMaxLength)
                return rs.SetError(Messages.Error.FieldTooLong("dirección", ValidationLimits.OrderAddressMaxLength));

            if (order.Date == default)
                return rs.SetError(Messages.Error.FieldRequired("fecha"));

            if (!string.IsNullOrEmpty(order.Phone) && order.Phone.Length > ValidationLimits.OrderPhoneMaxLength)
                return rs.SetError(Messages.Error.FieldTooLong("teléfono", ValidationLimits.OrderPhoneMaxLength));

            if (order.ShipmentPrice.HasValue && (order.ShipmentPrice.Value < 0 || order.ShipmentPrice.Value > ValidationLimits.OrderShipmentPriceMax))
                return rs.SetError(Messages.Error.InvalidField("flete"));

            if (!string.IsNullOrEmpty(order.Observation) && order.Observation.Length > ValidationLimits.OrderObservationMaxLength)
                return rs.SetError(Messages.Error.FieldTooLong("observación", ValidationLimits.OrderObservationMaxLength));

            return rs;
        }

        private static BaseResponse ValidateOrder(Order order)
        {
            var rs = new BaseResponse();
            var validation = ValidateOrder<object>(order);

            return validation.Error == null ? rs : rs.SetError(validation.Error.Message);
        }

        private async Task<BaseResponse<List<ProductOrder>>> BuildOrderProducts(
            List<OrderProductRequest> requestProducts,
            DateTime date,
            int? excludeOrderId = null,
            bool useRequestedUnitPrices = false)
        {
            var rs = new BaseResponse<List<ProductOrder>>();

            if (requestProducts == null || requestProducts.Count == 0)
                return rs.SetError(Messages.Error.OrderWithoutProducts());

            if (requestProducts.Any(x => x == null || x.ProductId <= 0))
                return rs.SetError(Messages.Error.FieldGraterThanZero("producto"));

            if (requestProducts.Any(x => x.Quantity <= 0 || x.Quantity > ValidationLimits.OrderQuantityMax))
                return rs.SetError(Messages.Error.FieldGraterThanZero("cantidad"));

            if (useRequestedUnitPrices && requestProducts.Any(x => x.UnitPrice.HasValue
                && (x.UnitPrice.Value <= 0 || x.UnitPrice.Value > ValidationLimits.ProductPriceMax)))
                return rs.SetError(Messages.Error.InvalidField("precio unitario"));

            var productIds = requestProducts.Select(x => x.ProductId).Distinct().ToList();
            var products = await _db.Product.Where(x => productIds.Contains(x.Id)).ToListAsync();

            if (products.Count != productIds.Count)
                return rs.SetError(Messages.Error.EntityNotFound("Producto"));

            var resolvedLines = requestProducts
                .Select(x =>
                {
                    var product = products.First(p => p.Id == x.ProductId);
                    return new
                    {
                        Product = product,
                        x.Quantity,
                        UnitPrice = useRequestedUnitPrices && x.UnitPrice.HasValue
                            ? x.UnitPrice.Value
                            : product.Price
                    };
                })
                .ToList();

            // Availability belongs to the product, not to a product-price line. All prices for the
            // same product are summed before comparing against stock.
            foreach (var productGroup in resolvedLines.GroupBy(x => x.Product.Id))
            {
                var product = productGroup.First().Product;
                var requestedQuantity = productGroup.Sum(x => (long)x.Quantity);

                var available = await GetAvailableStock(product, date, excludeOrderId);
                if (requestedQuantity > available)
                    return rs.SetError(Messages.Error.ProductWithoutStock());
            }

            var lines = resolvedLines
                .GroupBy(x => new { x.Product.Id, x.UnitPrice })
                .Select(x => new ProductOrder
                {
                    ProductId = x.Key.Id,
                    Product = x.First().Product,
                    Quantity = x.Sum(line => line.Quantity),
                    UnitPrice = x.Key.UnitPrice
                })
                .ToList();

            rs.Data = lines;
            return rs;
        }

        private static string ValidateBudgetRequest(BudgetRequest rq)
        {
            if (!string.IsNullOrEmpty(rq.ClientName) && rq.ClientName.Length > ValidationLimits.OrderClientNameMaxLength)
                return Messages.Error.FieldTooLong("cliente", ValidationLimits.OrderClientNameMaxLength);

            if (!string.IsNullOrEmpty(rq.EventAddress) && rq.EventAddress.Length > ValidationLimits.OrderAddressMaxLength)
                return Messages.Error.FieldTooLong("dirección", ValidationLimits.OrderAddressMaxLength);

            if (rq.ShipmentPrice.HasValue && (rq.ShipmentPrice.Value < 0 || rq.ShipmentPrice.Value > ValidationLimits.OrderShipmentPriceMax))
                return Messages.Error.InvalidField("flete");

            if (rq.Products.Any(x => x == null || x.ProductId <= 0))
                return Messages.Error.FieldGraterThanZero("producto");

            if (rq.Products.Any(x => x.Quantity <= 0 || x.Quantity > ValidationLimits.OrderQuantityMax))
                return Messages.Error.FieldGraterThanZero("cantidad");

            return null;
        }
        #endregion

        #region Helpers
        // What is left of a product on an event date once the orders of that day are discounted.
        private async Task<int> GetAvailableStock(Product product, DateTime date, int? excludeOrderId)
        {
            var dayStart = date.Date;
            var dayEnd = dayStart.AddDays(1);

            // Navigating through x.Order applies the soft-delete query filter, so cancelled orders release the stock they had reserved
            var reserved = await _db
                .ProductOrder
                .Where(x => x.ProductId == product.Id
                    && x.Order.Date >= dayStart
                    && x.Order.Date < dayEnd
                    && (!excludeOrderId.HasValue || x.OrderId != excludeOrderId.Value))
                .SumAsync(x => (int?)x.Quantity) ?? 0;

            return product.Stock - reserved;
        }

        private static DateTime NormalizeEventDate(DateTime date)
        {
            return date.Date.AddHours(EventDateHour);
        }

        private async Task<List<Order>> GetOrdersForDocument(GetAllRequest rq)
        {
            rq ??= new GetAllRequest();

            var query = _db
                .Order
                .Include(x => x.Products)
                .AsNoTracking()
                .AsQueryable();

            query = FilterQuery(query, rq);
            query = OrderQuery(query, rq);

            return await query.ToListAsync();
        }

        private BaseResponse<ReportFile> GenerateDetailReport(List<OrderReportRow> rows, bool withPrice, string fileName, List<ReportField> fields)
        {
            var columns = ReportColumns
                .For<OrderReportRow>()
                .Add(x => x.Description, "Producto", width: 3)
                .AddNumber(x => x.Quantity, "Cantidad")
                .AddIf(withPrice, x => Currency(x.UnitPrice), "Precio unitario", ReportAlignment.Right)
                .AddIf(withPrice, x => x.BreakPrice.HasValue ? Currency(x.BreakPrice.Value) : "-", "Precio por rotura", ReportAlignment.Right)
                .AddIf(withPrice, x => Currency(x.Subtotal), "Subtotal", ReportAlignment.Right)
                .Build();

            var summary = new List<ReportField>();
            if (withPrice)
                summary.Add(new ReportField("Total", Currency(rows.Sum(x => x.Subtotal))));

            return _reportService.GenerateTable(new ReportTableRequest<OrderReportRow>
            {
                FileName = fileName,
                Columns = columns,
                Rows = rows,
                Options = new ReportOptions
                {
                    Title = CompanyInfo.Name,
                    Subtitle = CompanyInfo.Slogan + Environment.NewLine + CompanyInfo.Tagline,
                    DocumentTitle = "DETALLE",
                    Logo = ReportAssets.Read("logo.png"),
                    ContactLines =
                    [
                        "CUIT: " + CompanyInfo.Cuit,
                        CompanyInfo.Street + " - Tel: " + CompanyInfo.Phone,
                        CompanyInfo.City
                    ],
                    Author = _tokenService.TryGetToken()?.Email,
                    FooterNote = CompanyInfo.Address + " - Tel: " + CompanyInfo.Phone,
                    WhatsAppQr = withPrice ? ReportAssets.Read("qr_wpp.png") : null,
                    InstagramQr = withPrice ? ReportAssets.Read("qr_ig.png") : null,
                    Filters = fields,
                    Summary = summary,
                    ShowRowCount = false
                }
            });
        }

        // The shipment is printed as one more line
        private static List<OrderReportRow> AppendShipmentRow(List<OrderReportRow> rows, decimal? shipmentPrice)
        {
            if (shipmentPrice.HasValue)
            {
                rows.Add(new OrderReportRow
                {
                    Description = ShipmentRowDescription,
                    Quantity = 1,
                    UnitPrice = shipmentPrice.Value,
                    BreakPrice = null,
                    Subtotal = shipmentPrice.Value
                });
            }

            return rows;
        }

        private static string Currency(decimal value)
        {
            return value.ToString("C2", ReportCulture.Current);
        }

        private static decimal CalculateTotal(Order order)
        {
            return order.Products.Sum(x => x.Quantity * x.UnitPrice) + (order.ShipmentPrice ?? 0);
        }

        // Products that were later removed from the catalogue must still show on the orders that used
        // them, so they are resolved with the soft-delete filter switched off. Letting EF include the
        // navigation instead would silently drop those lines and under-report the order total.
        private async Task<Dictionary<int, Product>> LoadLineProducts(IEnumerable<ProductOrder> lines)
        {
            var ids = lines.Select(x => x.ProductId).Distinct().ToList();

            return await _db
                .Product
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(x => ids.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id);
        }

        private static GetOneResponse MapOrder(Order order, Dictionary<int, Product> products)
        {
            var productsTotal = order.Products.Sum(x => x.Quantity * x.UnitPrice);

            return new GetOneResponse
            {
                Id = order.Id,
                ClientName = order.ClientName,
                Phone = order.Phone,
                EventAddress = order.EventAddress,
                Date = order.Date,
                ShipmentPrice = order.ShipmentPrice,
                Observation = order.Observation,
                ProductsTotal = productsTotal,
                Total = productsTotal + (order.ShipmentPrice ?? 0),
                CreatedAt = order.CreatedAt,
                UpdatedAt = order.UpdatedAt,
                Products = [.. order.Products
                    .Select(x => MapOrderProduct(x, products.GetValueOrDefault(x.ProductId)))
                    .OrderBy(x => x.Description)]
            };
        }

        private static OrderProductResponse MapOrderProduct(ProductOrder line, Product product)
        {
            return new OrderProductResponse
            {
                ProductId = line.ProductId,
                Description = product?.Description,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                BreakPrice = product?.BreakPrice,
                Subtotal = line.Quantity * line.UnitPrice
            };
        }

        private static List<ReportField> BuildReportFilters(GetAllRequest rq)
        {
            var filters = new List<ReportField>();

            if (rq == null)
                return filters;

            if (rq.Date.HasValue)
                filters.Add(new ReportField("Fecha", rq.Date.Value.ToString("dd/MM/yyyy")));

            if (rq.DateFrom.HasValue)
                filters.Add(new ReportField("Desde", rq.DateFrom.Value.ToString("dd/MM/yyyy")));

            if (rq.DateTo.HasValue)
                filters.Add(new ReportField("Hasta", rq.DateTo.Value.ToString("dd/MM/yyyy")));

            if (!string.IsNullOrWhiteSpace(rq.Search))
                filters.Add(new ReportField("Búsqueda", rq.Search));

            return filters;
        }

        // Stored dates carry a time component, so every date filter is a half-open day range rather
        // than an equality check
        private static IQueryable<Order> FilterQuery(IQueryable<Order> query, GetAllRequest rq)
        {
            if (rq.Date.HasValue)
            {
                var dayStart = rq.Date.Value.Date;
                var dayEnd = dayStart.AddDays(1);
                query = query.Where(x => x.Date >= dayStart && x.Date < dayEnd);
            }

            if (rq.DateFrom.HasValue)
            {
                var dateFrom = rq.DateFrom.Value.Date;
                query = query.Where(x => x.Date >= dateFrom);
            }

            if (rq.DateTo.HasValue)
            {
                var dateTo = rq.DateTo.Value.Date.AddDays(1);
                query = query.Where(x => x.Date < dateTo);
            }

            if (!string.IsNullOrWhiteSpace(rq.Search))
            {
                var search = rq.Search.Trim();
                query = query.Where(x => EF.Functions.Like(x.ClientName, "%" + search + "%")
                    || EF.Functions.Like(x.EventAddress, "%" + search + "%"));
            }

            return query;
        }

        private static IQueryable<Order> OrderQuery(IQueryable<Order> query, SortRequest rq)
        {
            return rq.ColumnSort switch
            {
                "clientName" => rq.SortDirection == SortDirectionCode.ASC ? query.OrderBy(x => x.ClientName) : query.OrderByDescending(x => x.ClientName),
                "createdAt" => rq.SortDirection == SortDirectionCode.ASC ? query.OrderBy(x => x.CreatedAt) : query.OrderByDescending(x => x.CreatedAt),
                "date" => rq.SortDirection == SortDirectionCode.ASC ? query.OrderBy(x => x.Date) : query.OrderByDescending(x => x.Date),
                _ => query.OrderByDescending(x => x.Date).ThenBy(x => x.ClientName),
            };
        }
        #endregion
    }
}
