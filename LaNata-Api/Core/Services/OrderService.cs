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
using LaNata_Api.Report.Models.Constants;
using LaNata_Api.Report.Models.Internal;
using LaNata_Api.Report.Services;

namespace LaNata_Api.Core.Services
{
    public class OrderService(APIContext context, AuditService auditService, ExportService exportService, ReportService reportService, TokenService tokenService)
    {
        private const int ClientNameMaxLength = 500;
        private const int AddressMaxLength = 500;
        private const int ObservationMaxLength = 500;
        private const int PhoneMaxLength = 20;
        private const double ShipmentPriceMax = 1000000;
        private const int QuantityMax = 100000;
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

            if (rq == null || rq.Id <= 0)
                return rs.SetError(Messages.Error.FieldGraterThanZero("id"));

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
                ClientName = TextHelper.ToTitleCase(rq.ClientName),
                Phone = TextHelper.ToNumber(rq.Phone),
                EventAddress = TextHelper.ToUpperFirst(rq.EventAddress),
                Date = NormalizeEventDate(rq.Date),
                ShipmentPrice = rq.ShipmentPrice,
                Observation = TextHelper.ToUpperFirst(rq.Observation)
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

            var order = await _db.Order.FirstOrDefaultAsync(x => x.Id == rq.Id);
            if (order == null)
                return rs.SetError(Messages.Error.EntityNotFound("Pedido"));

            var previous = order.Clone();

            order.ClientName = TextHelper.ToTitleCase(rq.ClientName);
            order.Phone = TextHelper.ToNumber(rq.Phone);
            order.EventAddress = TextHelper.ToUpperFirst(rq.EventAddress);
            order.Date = NormalizeEventDate(rq.Date);
            order.ShipmentPrice = rq.ShipmentPrice;
            order.Observation = TextHelper.ToUpperFirst(rq.Observation);
            order.UpdatedAt = DateTime.UtcNow;

            if (!rs.Attach(ValidateOrder(order)).Success)
                return rs;

            // Moving the order to another day re-checks availability there: the products it reserves
            // must still fit next to whatever the new date already has booked
            if (previous.Date.Date != order.Date.Date && !rs.Attach(await ValidateDateChange(order)).Success)
                return rs;

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (Exception)
            {
                return rs.SetError(Messages.Error.Exception());
            }

            await Audit(AuditActionCode.UPDATED, order, BuildAuditChanges(AuditChangeList.For(previous, order)));

            rs.Message = Messages.CRUD.EntityUpdated("Pedido");
            return rs;
        }

        public async Task<BaseResponse> Delete(DeleteRequest rq)
        {
            var rs = new BaseResponse();

            if (rq == null || rq.Id <= 0)
                return rs.SetError(Messages.Error.FieldGraterThanZero("id"));

            var order = await _db.Order.FirstOrDefaultAsync(x => x.Id == rq.Id);

            if (order == null)
                return rs.SetError(Messages.Error.EntityNotFound("Pedido"));

            // Soft delete on the order only, as the legacy did. The lines stay: they have no
            // deleted_at column, and the stock they reserve is released anyway because every
            // availability query reaches them through Order, which is filtered.
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
        // Days that already hold at least one order, for the calendar on the orders screen
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

            // Grouped on the date part: stored rows carry a time, and the calendar wants one entry per day regardless of it
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

        public async Task<BaseResponse> AddProduct(AddProductRequest rq)
        {
            var rs = new BaseResponse();

            if (rq == null || rq.OrderId <= 0)
                return rs.SetError(Messages.Error.FieldGraterThanZero("pedido"));

            if (rq.ProductId <= 0)
                return rs.SetError(Messages.Error.FieldGraterThanZero("producto"));

            if (rq.Quantity <= 0)
                return rs.SetError(Messages.Error.FieldGraterThanZero("cantidad"));

            if (rq.Quantity > QuantityMax)
                return rs.SetError(Messages.Error.InvalidField("cantidad"));

            var order = await _db
                .Order
                .Include(x => x.Products)
                .FirstOrDefaultAsync(x => x.Id == rq.OrderId);

            if (order == null)
                return rs.SetError(Messages.Error.EntityNotFound("Pedido"));

            var product = await _db.Product.FirstOrDefaultAsync(x => x.Id == rq.ProductId);
            if (product == null)
                return rs.SetError(Messages.Error.EntityNotFound("Producto"));

            var available = await GetAvailableStock(product, order.Date, order.Id);
            if (rq.Quantity > available)
                return rs.SetError(Messages.Error.ProductWithoutStock());

            // The price identifies the line, which is what lets one order hold the same product at an old and a current price at the same time
            var unitPrice = rq.UnitPrice ?? product.Price;
            var line = order.Products.FirstOrDefault(x => x.ProductId == product.Id && x.UnitPrice == unitPrice);
            var isNewLine = line == null;

            if (isNewLine)
            {
                line = new ProductOrder
                {
                    OrderId = order.Id,
                    ProductId = product.Id,
                    Quantity = rq.Quantity,
                    UnitPrice = unitPrice
                };

                _db.ProductOrder.Add(line);
            }
            else
            {
                if (line.Quantity + rq.Quantity > QuantityMax)
                    return rs.SetError(Messages.Error.InvalidField("cantidad"));

                line.Quantity += rq.Quantity;
            }

            // Touching a line is an edit of the order it belongs to
            order.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return rs.SetError(Messages.Error.ProductAlreadyInOrder());
            }
            catch (Exception)
            {
                return rs.SetError(Messages.Error.Exception());
            }

            await AuditProductLine(AuditActionCode.PRODUCT_ADDED, order, product, rq.Quantity, unitPrice);

            rs.Message = isNewLine ? Messages.Order.ProductAdded() : Messages.Order.ProductQuantityAdded();
            return rs;
        }

        public async Task<BaseResponse> RemoveProduct(RemoveProductRequest rq)
        {
            var rs = new BaseResponse();

            if (rq == null || rq.OrderId <= 0)
                return rs.SetError(Messages.Error.FieldGraterThanZero("pedido"));

            if (rq.ProductId <= 0)
                return rs.SetError(Messages.Error.FieldGraterThanZero("producto"));

            if (rq.Quantity <= 0)
                return rs.SetError(Messages.Error.FieldGraterThanZero("cantidad"));

            var order = await _db.Order.FirstOrDefaultAsync(x => x.Id == rq.OrderId);
            if (order == null)
                return rs.SetError(Messages.Error.EntityNotFound("Pedido"));

            // No Include on Product on purpose: a line whose product was removed from the catalogue must still be removable from the order
            var line = await _db
                .ProductOrder
                .FirstOrDefaultAsync(x => x.OrderId == rq.OrderId && x.ProductId == rq.ProductId && x.UnitPrice == rq.UnitPrice);

            if (line == null)
                return rs.SetError(Messages.Error.ProductNotInOrder());

            if (rq.Quantity > line.Quantity)
                return rs.SetError(Messages.Error.QuantityGreaterThanReserved());

            var isFullRemoval = rq.Quantity == line.Quantity;
            var product = (await LoadLineProducts([line])).GetValueOrDefault(line.ProductId);

            // Hard delete, like the legacy: the join table has no deleted_at to soft delete into
            if (isFullRemoval)
                _db.ProductOrder.Remove(line);
            else
                line.Quantity -= rq.Quantity;

            order.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (Exception)
            {
                return rs.SetError(Messages.Error.Exception());
            }

            await AuditProductLine(AuditActionCode.PRODUCT_REMOVED, order, product, rq.Quantity, rq.UnitPrice);

            rs.Message = isFullRemoval ? Messages.Order.ProductRemoved() : Messages.Order.ProductQuantitySubtracted();
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

        // The order detail the legacy printed: the same document with or without prices
        public async Task<BaseResponse<ReportFile>> Report(ReportRequest rq)
        {
            var rs = new BaseResponse<ReportFile>();

            if (rq == null || rq.Id <= 0)
                return rs.SetError(Messages.Error.FieldGraterThanZero("id"));

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

        // Quote for an order that has not been created yet
        public async Task<BaseResponse<ReportFile>> Budget(BudgetRequest rq)
        {
            var rs = new BaseResponse<ReportFile>();

            if (rq == null || rq.Products == null || rq.Products.Count == 0)
                return rs.SetError(Messages.Error.OrderWithoutProducts());

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
                new("Cliente", TextHelper.ToTitleCase(rq.ClientName) ?? "-"),
                new("Fecha", rq.Date == default ? "-" : rq.Date.ToString("dd/MM/yyyy")),
                new("Lugar", TextHelper.ToUpperFirst(rq.EventAddress) ?? "-")
            });

            if (rs.Success)
            {
                await AuditDocument(AuditActionCode.REPORT_GENERATED, rs.Data.FileName, rows.Count, "Pdf",
                [
                    new("Presupuesto", TextHelper.ToTitleCase(rq.ClientName) ?? "-")
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

        private async Task AuditProductLine(string actionCode, Order order, Product product, int quantity, double unitPrice)
        {
            await _auditService.Audit(new AuditRequest
            {
                ActionCode = actionCode,
                Entity = AuditEntityCode.ORDER,
                EntityKey = order.Id.ToString(),
                Name = order.ClientName,
                Payload = new Dictionary<string, object>
                {
                    ["Producto"] = product?.Description,
                    ["Cantidad"] = quantity,
                    ["Precio unitario"] = unitPrice
                }
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

            if (order.ClientName.Length > ClientNameMaxLength)
                return rs.SetError(Messages.Error.FieldTooLong("cliente", ClientNameMaxLength));

            if (string.IsNullOrWhiteSpace(order.EventAddress))
                return rs.SetError(Messages.Error.FieldRequired("dirección"));

            if (order.EventAddress.Length > AddressMaxLength)
                return rs.SetError(Messages.Error.FieldTooLong("dirección", AddressMaxLength));

            if (order.Date == default)
                return rs.SetError(Messages.Error.FieldRequired("fecha"));

            if (!string.IsNullOrEmpty(order.Phone) && order.Phone.Length > PhoneMaxLength)
                return rs.SetError(Messages.Error.FieldTooLong("teléfono", PhoneMaxLength));

            if (order.ShipmentPrice.HasValue && (order.ShipmentPrice.Value < 0 || order.ShipmentPrice.Value > ShipmentPriceMax))
                return rs.SetError(Messages.Error.InvalidField("flete"));

            if (!string.IsNullOrEmpty(order.Observation) && order.Observation.Length > ObservationMaxLength)
                return rs.SetError(Messages.Error.FieldTooLong("observación", ObservationMaxLength));

            return rs;
        }

        private static BaseResponse ValidateOrder(Order order)
        {
            var rs = new BaseResponse();
            var validation = ValidateOrder<object>(order);

            return validation.Error == null ? rs : rs.SetError(validation.Error.Message);
        }

        private async Task<BaseResponse> ValidateDateChange(Order order)
        {
            var rs = new BaseResponse();

            var lines = await _db
                .ProductOrder
                .Where(x => x.OrderId == order.Id)
                .ToListAsync();

            var products = await LoadLineProducts(lines);

            foreach (var group in lines.GroupBy(x => x.ProductId))
            {
                var product = products.GetValueOrDefault(group.Key);
                if (product == null)
                    continue;

                var available = await GetAvailableStock(product, order.Date, order.Id);

                if (group.Sum(x => x.Quantity) > available)
                    return rs.SetError(Messages.Error.ProductWithoutStock());
            }

            return rs;
        }

        private async Task<BaseResponse<List<ProductOrder>>> BuildOrderProducts(List<OrderProductRequest> requestProducts, DateTime date)
        {
            var rs = new BaseResponse<List<ProductOrder>>();

            if (requestProducts == null || requestProducts.Count == 0)
                return rs.SetError(Messages.Error.OrderWithoutProducts());

            if (requestProducts.Any(x => x == null || x.ProductId <= 0))
                return rs.SetError(Messages.Error.FieldGraterThanZero("producto"));

            if (requestProducts.Any(x => x.Quantity <= 0 || x.Quantity > QuantityMax))
                return rs.SetError(Messages.Error.FieldGraterThanZero("cantidad"));

            // The same product can arrive more than once in the basket, so it is summed before the
            // stock check instead of being validated line by line
            var requested = requestProducts
                .GroupBy(x => x.ProductId)
                .Select(x => new
                {
                    ProductId = x.Key,
                    Quantity = x.Sum(p => p.Quantity)
                })
                .ToList();

            var productIds = requested.Select(x => x.ProductId).ToList();
            var products = await _db.Product.Where(x => productIds.Contains(x.Id)).ToListAsync();

            var lines = new List<ProductOrder>();

            foreach (var item in requested)
            {
                var product = products.FirstOrDefault(x => x.Id == item.ProductId);
                if (product == null)
                    return rs.SetError(Messages.Error.EntityNotFound("Producto"));

                var available = await GetAvailableStock(product, date, null);
                if (item.Quantity > available)
                    return rs.SetError(Messages.Error.ProductWithoutStock());

                lines.Add(new ProductOrder
                {
                    ProductId = product.Id,
                    Product = product,
                    Quantity = item.Quantity,
                    UnitPrice = product.Price
                });
            }

            rs.Data = lines;
            return rs;
        }
        #endregion

        #region Helpers
        // What is left of a product on an event date once the orders of that day are discounted.
        // excludeOrderId keeps the order being edited out of the count, so its own reservation is
        // not read as somebody else's.
        private async Task<long> GetAvailableStock(Product product, DateTime date, int? excludeOrderId)
        {
            var dayStart = date.Date;
            var dayEnd = dayStart.AddDays(1);

            // Navigating through x.Order applies the soft-delete query filter, so cancelled orders
            // release the stock they had reserved
            var reserved = await _db
                .ProductOrder
                .Where(x => x.ProductId == product.Id
                    && x.Order.Date >= dayStart
                    && x.Order.Date < dayEnd
                    && (!excludeOrderId.HasValue || x.OrderId != excludeOrderId.Value))
                .SumAsync(x => (long?)x.Quantity) ?? 0;

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

        // The shipment is printed as one more line, the way the legacy detail did it
        private static List<OrderReportRow> AppendShipmentRow(List<OrderReportRow> rows, double? shipmentPrice)
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

        private static string Currency(double value)
        {
            return value.ToString("C2", ReportCulture.Current);
        }

        private static double CalculateTotal(Order order)
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
                BreakPrice = product?.BreakPrice ?? 0,
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
