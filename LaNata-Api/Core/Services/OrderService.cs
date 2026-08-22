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
        private const string NoVariant = "Sin variante";
        private const string DraftStateName = "Presupuesto";
        private const string ConfirmedStateName = "Confirmado";

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
                            IsDraft = x.IsDraft,
                            PaidAt = x.PaidAt,
                            PaidByEmail = x.PaidByUser.Email,
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
                .Include(x => x.PaidByUser)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == rq.Id);

            if (order == null)
                return rs.SetError(Messages.Error.EntityNotFound("Pedido"));

            rs.Data = MapOrder(order, await LoadLineProducts(order.Products), await LoadLineVariants(order.Products));
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
                Observation = rq.Observation?.Trim(),
                IsDraft = rq.IsDraft
            };

            if (!rs.Attach(ValidateOrder<CreateResponse>(order)).Success)
                return rs;

            // A draft reserves nothing, so it is not measured against the date. Confirm is where the
            // availability is checked, and it reports every shortage at once instead of the first
            var linesResponse = await BuildOrderProducts(rq.Products, order.Date, validateStock: !order.IsDraft);
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

            await Audit(AuditActionCode.CREATED, order, BuildCreateAuditChanges(order));

            rs.Message = Messages.CRUD.EntityCreated(EntityName(order));
            rs.Data = new CreateResponse
            {
                Id = order.Id,
                ClientName = order.ClientName,
                Date = order.Date,
                Total = CalculateTotal(order),
                IsDraft = order.IsDraft,
                Products = [.. order.Products.Select(x => MapOrderProduct(x, x.Product, x.Variant))]
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

            // IsDraft is not part of the request: a quote becomes an order through Confirm, which is
            // the path that checks the stock. Editing a draft leaves it a draft.
            var linesResponse = await BuildOrderProducts(rq.Products, order.Date, order.Id, useRequestedUnitPrices: true, validateStock: !order.IsDraft);
            if (!linesResponse.Success)
                return rs.SetError(linesResponse.Error.Message);

            var auditLines = previous.Products.Concat(linesResponse.Data).ToList();
            var auditProducts = await LoadLineProducts(auditLines);
            var auditVariants = await LoadLineVariants(auditLines);
            var auditChanges = BuildUpdateAuditChanges(previous, order, linesResponse.Data, auditProducts, auditVariants);

            var requestedLines = linesResponse.Data.ToDictionary(x => (x.ProductId, x.UnitPrice, x.VariantId));

            foreach (var existingLine in order.Products.ToList())
            {
                if (requestedLines.Remove((existingLine.ProductId, existingLine.UnitPrice, existingLine.VariantId), out var requestedLine))
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

            rs.Message = Messages.CRUD.EntityUpdated(EntityName(order));
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

            rs.Message = Messages.CRUD.EntityDeleted(EntityName(order));
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

        public async Task<BaseResponse<ConfirmResponse>> Confirm(ConfirmRequest rq)
        {
            var rs = new BaseResponse<ConfirmResponse>();

            if (rq == null || rq.Id <= 0)
                return rs.SetError(Messages.Error.FieldGraterThanZero("id"));

            var order = await _db
                .Order
                .Include(x => x.Products)
                .FirstOrDefaultAsync(x => x.Id == rq.Id);

            if (order == null)
                return rs.SetError(Messages.Error.EntityNotFound("Pedido"));

            if (!order.IsDraft)
                return rs.SetError(Messages.Error.OrderAlreadyConfirmed());

            if (order.Products.Count == 0)
                return rs.SetError(Messages.Error.OrderWithoutProducts());

            var missing = await GetMissingStock(order);
            if (missing.Count > 0)
            {
                rs.Data = new ConfirmResponse { Id = order.Id, Missing = missing };
                return rs.SetError(Messages.Error.OrderWithoutStockToConfirm());
            }

            var previous = order.Clone();

            order.IsDraft = false;

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (Exception)
            {
                return rs.SetError(Messages.Error.Exception());
            }

            await Audit(AuditActionCode.ORDER_CONFIRMED, order, BuildAuditChanges(AuditChangeList.For(previous, order)));

            rs.Message = Messages.CRUD.EntityConfirmed("Pedido");
            rs.Data = new ConfirmResponse { Id = order.Id };
            return rs;
        }

        public async Task<BaseResponse> MarkAsPaid(MarkAsPaidRequest rq)
        {
            return await SetPaid(rq?.Id ?? 0, true);
        }

        public async Task<BaseResponse> MarkAsUnpaid(MarkAsUnpaidRequest rq)
        {
            return await SetPaid(rq?.Id ?? 0, false);
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
                .Add("Estado", x => x.IsDraft ? DraftStateName : ConfirmedStateName)
                .Add(x => x.ShipmentPrice, "Flete", ExportValueFormat.Currency)
                .Add("Total", x => CalculateTotal(x), ExportValueFormat.Currency)
                .Add(x => x.PaidAt, "Cobrado", ExportValueFormat.DateTime)
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
            var lineVariants = await LoadLineVariants(order.Products);

            var productRows = order.Products
                .Select(x =>
                {
                    var product = lineProducts.GetValueOrDefault(x.ProductId);
                    var variant = x.VariantId.HasValue ? lineVariants.GetValueOrDefault(x.VariantId.Value) : null;

                    return new OrderReportRow
                    {
                        Description = variant == null ? product?.Description : product?.Description + " (" + variant.Description + ")",
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

            // A draft is the document that gets sent to the client to be approved, so it prints as a
            // presupuesto and says so; confirming it is what turns the same rows into a pedido
            if (order.IsDraft)
                fields.Add(new ReportField("Estado", DraftStateName + " pendiente de confirmación"));

            rs = GenerateDetailReport(rows, rq.WithPrice, "pedido_" + order.Id, fields, isBudget: order.IsDraft);

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

            var variantIds = rq.Products
                .Where(x => x.VariantId.HasValue)
                .Select(x => x.VariantId.Value)
                .Distinct()
                .ToList();

            // No IgnoreQueryFilters here, unlike the order paths: a quote prices something that has
            // not been booked yet, so a retired variant must not be quotable
            var variants = variantIds.Count == 0
                ? []
                : await _db.ProductVariant.AsNoTracking().Where(x => variantIds.Contains(x.Id)).ToListAsync();

            if (variants.Count != variantIds.Count)
                return rs.SetError(Messages.Error.EntityNotFound("Variante", femine: true));

            var productRows = new List<OrderReportRow>();

            // A variant is its own row at its own price: the surcharge rides on top of the product
            // price, exactly as the order resolves it when it is finally created
            foreach (var requested in rq.Products.GroupBy(x => new { x.ProductId, x.VariantId }))
            {
                var product = products.FirstOrDefault(x => x.Id == requested.Key.ProductId);
                if (product == null)
                    return rs.SetError(Messages.Error.EntityNotFound("Producto"));

                var variant = requested.Key.VariantId.HasValue
                    ? variants.First(x => x.Id == requested.Key.VariantId.Value)
                    : null;

                if (variant != null && variant.ProductId != product.Id)
                    return rs.SetError(Messages.Error.VariantNotFromProduct());

                var quantity = requested.Sum(x => x.Quantity);
                if (quantity <= 0)
                    return rs.SetError(Messages.Error.FieldGraterThanZero("cantidad"));

                var unitPrice = product.Price + (variant?.PriceExtra ?? 0);

                productRows.Add(new OrderReportRow
                {
                    Description = variant == null ? product.Description : product.Description + " (" + variant.Description + ")",
                    Quantity = quantity,
                    UnitPrice = unitPrice,
                    BreakPrice = product.BreakPrice,
                    Subtotal = quantity * unitPrice
                });
            }

            var rows = AppendShipmentRow([.. productRows.OrderBy(x => x.Description)], rq.ShipmentPrice);

            // The footer of this document prints the address instead of the generation stamp, so the
            // note carries the deadline itself rather than pointing at a date the client cannot see
            var validUntil = DateTime.UtcNow.Date.AddDays(CompanyInfo.BudgetValidityDays).ToString("dd/MM/yyyy");

            rs = GenerateDetailReport(rows, withPrice: true, "presupuesto", new List<ReportField>
            {
                new("Cliente", rq.ClientName?.Trim() ?? "-"),
                new("Fecha", rq.Date == default ? "-" : rq.Date.ToString("dd/MM/yyyy")),
                new("Lugar", rq.EventAddress?.Trim() ?? "-")
            }, Messages.Report.BudgetValidity(CompanyInfo.BudgetValidityDays, validUntil), isBudget: true);

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
                .AddField(x => x.IsDraft, "Presupuesto", AuditFormat.YesNo)
                .AddField(x => x.PaidAt, "Cobro", AuditFormat.Date)
                .Build();
        }

        private static AuditChangeSet BuildCreateAuditChanges(Order order)
        {
            var changes = BuildAuditChanges(AuditChangeList.ForCreated(order));
            var lines = order.Products ?? [];
            var products = lines
                .Where(x => x.Product != null)
                .Select(x => x.Product)
                .DistinctBy(x => x.Id)
                .ToDictionary(x => x.Id);
            var variants = lines
                .Where(x => x.Variant != null)
                .Select(x => x.Variant)
                .DistinctBy(x => x.Id)
                .ToDictionary(x => x.Id);

            changes.Payload[nameof(Order.Products)] = BuildProductAuditSnapshot(lines, products, variants);
            return changes;
        }

        private static AuditChangeSet BuildUpdateAuditChanges(
            Order previous,
            Order updated,
            IEnumerable<ProductOrder> updatedProducts,
            IReadOnlyDictionary<int, Product> products,
            IReadOnlyDictionary<int, ProductVariant> variants)
        {
            var changes = BuildAuditChanges(AuditChangeList.For(previous, updated));
            var previousLines = previous.Products ?? [];
            var currentLines = updatedProducts?.ToList() ?? [];

            changes.State["Products"] = BuildProductAuditSnapshot(previousLines, products, variants);
            changes.Payload["Products"] = BuildProductAuditSnapshot(currentLines, products, variants);

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
            IReadOnlyDictionary<int, Product> products,
            IReadOnlyDictionary<int, ProductVariant> variants)
        {
            return lines
                .Select(x =>
                {
                    products.TryGetValue(x.ProductId, out var product);

                    ProductVariant variant = null;
                    if (x.VariantId.HasValue)
                        variants.TryGetValue(x.VariantId.Value, out variant);

                    return new
                    {
                        Line = x,
                        ProductName = product?.Description ?? $"Producto #{x.ProductId}",
                        VariantName = variant?.Description ?? (x.VariantId.HasValue ? $"Variante #{x.VariantId.Value}" : NoVariant)
                    };
                })
                .OrderBy(x => x.ProductName)
                .ThenBy(x => x.Line.ProductId)
                .ThenBy(x => x.Line.UnitPrice)
                .ThenBy(x => x.Line.VariantId)
                .Select(x => new Dictionary<string, object>
                {
                    ["Product"] = $"#{x.Line.ProductId} - {x.ProductName}",
                    ["Variant"] = x.VariantName,
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
                .ThenBy(x => x.VariantId)
                .Select(x => (x.ProductId, x.Quantity, x.UnitPrice, x.VariantId));
            var updatedValues = updated
                .OrderBy(x => x.ProductId)
                .ThenBy(x => x.UnitPrice)
                .ThenBy(x => x.VariantId)
                .Select(x => (x.ProductId, x.Quantity, x.UnitPrice, x.VariantId));

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

            if (order.ShipmentPrice.HasValue && order.ShipmentPrice.Value < 0)
                return rs.SetError(Messages.Error.InvalidField("flete"));

            if (order.ShipmentPrice > ValidationLimits.OrderShipmentPriceMax)
                return rs.SetError(Messages.Error.FieldMax("flete", ValidationLimits.OrderShipmentPriceMax));

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

        // Both ceilings for an order already saved, reported in full rather than stopping at the first
        // shortage. Products and variants are resolved with the soft-delete filter off, the same as
        // every other read of what an order line points at.
        private async Task<List<ConfirmResponse.MissingStockItem>> GetMissingStock(Order order)
        {
            var lines = order.Products;
            var products = await LoadLineProducts(lines);
            var variants = await LoadLineVariants(lines);

            var productIds = lines.Select(x => x.ProductId).Distinct().ToList();
            var variantIds = lines.Where(x => x.VariantId.HasValue).Select(x => x.VariantId.Value).Distinct().ToList();

            var reserved = await GetReservations(productIds, variantIds, order.Date, order.Id);
            var missing = new List<ConfirmResponse.MissingStockItem>();

            foreach (var group in lines.GroupBy(x => x.ProductId))
            {
                var product = products.GetValueOrDefault(group.Key);
                var requested = group.Sum(x => x.Quantity);
                var available = (product?.Stock ?? 0) - reserved.ForProduct(group.Key);

                if (requested > available)
                {
                    missing.Add(new ConfirmResponse.MissingStockItem
                    {
                        ProductId = group.Key,
                        Description = product?.Description ?? "Producto #" + group.Key,
                        RequestedQuantity = requested,
                        AvailableStock = available
                    });
                }
            }

            foreach (var group in lines.Where(x => x.VariantId.HasValue).GroupBy(x => new { x.ProductId, VariantId = x.VariantId.Value }))
            {
                var variant = variants.GetValueOrDefault(group.Key.VariantId);
                var requested = group.Sum(x => x.Quantity);
                var available = (variant?.Stock ?? 0) - reserved.ForVariant(group.Key.VariantId);

                if (requested > available)
                {
                    missing.Add(new ConfirmResponse.MissingStockItem
                    {
                        ProductId = group.Key.ProductId,
                        Description = products.GetValueOrDefault(group.Key.ProductId)?.Description ?? "Producto #" + group.Key.ProductId,
                        VariantId = group.Key.VariantId,
                        VariantDescription = variant?.Description ?? "Variante #" + group.Key.VariantId,
                        RequestedQuantity = requested,
                        AvailableStock = available
                    });
                }
            }

            return [.. missing.OrderBy(x => x.Description).ThenBy(x => x.VariantDescription)];
        }

        private async Task<BaseResponse<List<ProductOrder>>> BuildOrderProducts(
            List<OrderProductRequest> requestProducts,
            DateTime date,
            int? excludeOrderId = null,
            bool useRequestedUnitPrices = false,
            bool validateStock = true)
        {
            var rs = new BaseResponse<List<ProductOrder>>();

            if (requestProducts == null || requestProducts.Count == 0)
                return rs.SetError(Messages.Error.OrderWithoutProducts());

            if (requestProducts.Any(x => x == null || x.ProductId <= 0))
                return rs.SetError(Messages.Error.FieldGraterThanZero("producto"));

            if (requestProducts.Any(x => x.Quantity <= 0))
                return rs.SetError(Messages.Error.FieldGraterThanZero("cantidad"));

            if (requestProducts.Any(x => x.Quantity > ValidationLimits.OrderQuantityMax))
                return rs.SetError(Messages.Error.FieldMax("cantidad", ValidationLimits.OrderQuantityMax));

            if (useRequestedUnitPrices && requestProducts.Any(x => x.UnitPrice.HasValue && x.UnitPrice.Value <= 0))
                return rs.SetError(Messages.Error.InvalidField("precio unitario"));

            if (useRequestedUnitPrices && requestProducts.Any(x => x.UnitPrice > ValidationLimits.ProductPriceMax))
                return rs.SetError(Messages.Error.FieldMax("precio unitario", ValidationLimits.ProductPriceMax));

            var productIds = requestProducts.Select(x => x.ProductId).Distinct().ToList();
            var products = await _db.Product.Where(x => productIds.Contains(x.Id)).ToListAsync();

            if (products.Count != productIds.Count)
                return rs.SetError(Messages.Error.EntityNotFound("Producto"));

            var variantIds = requestProducts
                .Where(x => x.VariantId.HasValue)
                .Select(x => x.VariantId.Value)
                .Distinct()
                .ToList();

            // IgnoreQueryFilters on purpose: an order being re-saved may hold a line on a variant
            // that was retired since, and loading it with the filter on would make an otherwise
            // innocent edit unsavable. Same class of bug as the soft deleted product behind a line.
            var variants = variantIds.Count == 0
                ? []
                : await _db
                    .ProductVariant
                    .IgnoreQueryFilters()
                    .Where(x => variantIds.Contains(x.Id))
                    .ToListAsync();

            if (variants.Count != variantIds.Count)
                return rs.SetError(Messages.Error.EntityNotFound("Variante", femine: true));

            if (requestProducts.Any(x => x.VariantId.HasValue
                && variants.First(v => v.Id == x.VariantId.Value).ProductId != x.ProductId))
                return rs.SetError(Messages.Error.VariantNotFromProduct());

            var resolvedLines = requestProducts
                .Select(x =>
                {
                    var product = products.First(p => p.Id == x.ProductId);
                    var variant = x.VariantId.HasValue ? variants.First(v => v.Id == x.VariantId.Value) : null;

                    return new
                    {
                        Product = product,
                        Variant = variant,
                        x.Quantity,
                        UnitPrice = useRequestedUnitPrices && x.UnitPrice.HasValue
                            ? x.UnitPrice.Value
                            : product.Price + (variant?.PriceExtra ?? 0)
                    };
                })
                .ToList();

            if (validateStock)
            {
                var reserved = await GetReservations(productIds, variantIds, date, excludeOrderId);

                foreach (var productGroup in resolvedLines.GroupBy(x => x.Product.Id))
                {
                    var product = productGroup.First().Product;
                    var requestedQuantity = productGroup.Sum(x => (long)x.Quantity);

                    if (requestedQuantity > product.Stock - reserved.ForProduct(product.Id))
                        return rs.SetError(Messages.Error.ProductWithoutStock());
                }

                foreach (var variantGroup in resolvedLines.Where(x => x.Variant != null).GroupBy(x => x.Variant.Id))
                {
                    var variant = variantGroup.First().Variant;
                    var requestedQuantity = variantGroup.Sum(x => (long)x.Quantity);

                    if (requestedQuantity > variant.Stock - reserved.ForVariant(variant.Id))
                        return rs.SetError(Messages.Error.VariantWithoutStock(variant.Description));
                }
            }

            var lines = resolvedLines
                .GroupBy(x => new { x.Product.Id, x.UnitPrice, VariantId = x.Variant == null ? (int?)null : x.Variant.Id })
                .Select(x => new ProductOrder
                {
                    ProductId = x.Key.Id,
                    Product = x.First().Product,
                    Quantity = x.Sum(line => line.Quantity),
                    UnitPrice = x.Key.UnitPrice,
                    VariantId = x.Key.VariantId,
                    Variant = x.First().Variant
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
        private async Task<BaseResponse> SetPaid(int id, bool paid)
        {
            var rs = new BaseResponse();

            if (id <= 0)
                return rs.SetError(Messages.Error.FieldGraterThanZero("id"));

            var order = await _db.Order.FirstOrDefaultAsync(x => x.Id == id);
            if (order == null)
                return rs.SetError(Messages.Error.EntityNotFound("Pedido"));

            // Nothing to collect on a quote nobody has accepted yet
            if (paid && order.IsDraft)
                return rs.SetError(Messages.Error.OrderDraftCannotBePaid());

            if (paid && order.PaidAt.HasValue)
                return rs.SetError(Messages.Error.OrderAlreadyPaid());

            if (!paid && !order.PaidAt.HasValue)
                return rs.SetError(Messages.Error.OrderNotPaid());

            var previous = order.Clone();

            order.PaidAt = paid ? DateTime.UtcNow : null;
            order.PaidBy = paid ? _tokenService.TryGetToken()?.UserId : null;

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (Exception)
            {
                return rs.SetError(Messages.Error.Exception());
            }

            await Audit(
                paid ? AuditActionCode.ORDER_PAID : AuditActionCode.ORDER_UNPAID,
                order,
                BuildAuditChanges(AuditChangeList.For(previous, order)));

            rs.Message = paid ? Messages.CRUD.EntityPaid("Pedido") : Messages.CRUD.EntityUnpaid("Pedido");
            return rs;
        }

        // What is left of a product on an event date once the confirmed orders of that day are
        // discounted. Two round trips whatever the order holds, rather than one query per product.
        // ExcludeOrderId leaves the order being edited out of the count, so its own reservation does
        // not block it. Drafts are skipped: a quote holds nothing until somebody confirms it.
        private async Task<StockReservation> GetReservations(List<int> productIds, List<int> variantIds, DateTime date, int? excludeOrderId)
        {
            var reservation = new StockReservation();

            if (productIds.Count == 0)
                return reservation;

            var dayStart = date.Date;
            var dayEnd = dayStart.AddDays(1);

            // Navigating through x.Order applies the soft-delete query filter, so cancelled orders release the stock they had reserved
            var query = _db
                .ProductOrder
                .Where(x => x.Order.Date >= dayStart
                    && x.Order.Date < dayEnd
                    && !x.Order.IsDraft
                    && (!excludeOrderId.HasValue || x.OrderId != excludeOrderId.Value));

            reservation.ByProduct = await query
                .Where(x => productIds.Contains(x.ProductId))
                .GroupBy(x => x.ProductId)
                .Select(x => new { ProductId = x.Key, Quantity = x.Sum(line => line.Quantity) })
                .ToDictionaryAsync(x => x.ProductId, x => x.Quantity);

            if (variantIds.Count == 0)
                return reservation;

            reservation.ByVariant = await query
                .Where(x => x.VariantId.HasValue && variantIds.Contains(x.VariantId.Value))
                .GroupBy(x => x.VariantId.Value)
                .Select(x => new { VariantId = x.Key, Quantity = x.Sum(line => line.Quantity) })
                .ToDictionaryAsync(x => x.VariantId, x => x.Quantity);

            return reservation;
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

        private BaseResponse<ReportFile> GenerateDetailReport(List<OrderReportRow> rows, bool withPrice, string fileName, List<ReportField> fields, string note = null, bool isBudget = false)
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
                    DocumentTitle = isBudget ? "PRESUPUESTO" : "DETALLE",
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
                    Note = note,
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

        // A draft is called "presupuesto" everywhere the user reads it, so the confirmation toast of a
        // quote does not talk about a pedido that does not exist yet
        private static string EntityName(Order order)
        {
            return order.IsDraft ? DraftStateName : "Pedido";
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

        private async Task<Dictionary<int, ProductVariant>> LoadLineVariants(IEnumerable<ProductOrder> lines)
        {
            var ids = lines.Where(x => x.VariantId.HasValue).Select(x => x.VariantId.Value).Distinct().ToList();

            if (ids.Count == 0)
                return [];

            return await _db
                .ProductVariant
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(x => ids.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id);
        }

        private static GetOneResponse MapOrder(Order order, Dictionary<int, Product> products, Dictionary<int, ProductVariant> variants)
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
                IsDraft = order.IsDraft,
                PaidAt = order.PaidAt,
                PaidByEmail = order.PaidByUser?.Email,
                CreatedAt = order.CreatedAt,
                UpdatedAt = order.UpdatedAt,
                Products = [.. order.Products
                    .Select(x => MapOrderProduct(
                        x,
                        products.GetValueOrDefault(x.ProductId),
                        x.VariantId.HasValue ? variants.GetValueOrDefault(x.VariantId.Value) : null))
                    .OrderBy(x => x.Description)
                    .ThenBy(x => x.VariantDescription)]
            };
        }

        private static OrderProductResponse MapOrderProduct(ProductOrder line, Product product, ProductVariant variant)
        {
            return new OrderProductResponse
            {
                Id = line.Id,
                ProductId = line.ProductId,
                Description = product?.Description,
                VariantId = line.VariantId,
                VariantDescription = variant?.Description,
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

            if (rq.Paid.HasValue)
                filters.Add(new ReportField("Cobro", rq.Paid.Value ? "Cobrados" : "Pendientes"));

            if (rq.IsDraft.HasValue)
                filters.Add(new ReportField("Estado", rq.IsDraft.Value ? DraftStateName : ConfirmedStateName));

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

            if (rq.Paid.HasValue)
                query = rq.Paid.Value ? query.Where(x => x.PaidAt != null) : query.Where(x => x.PaidAt == null);

            if (rq.IsDraft.HasValue)
            {
                var isDraft = rq.IsDraft.Value;
                query = query.Where(x => x.IsDraft == isDraft);
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
