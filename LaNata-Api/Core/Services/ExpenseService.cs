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
using LaNata_Api.Core.Helpers.Documents;
using LaNata_Api.Core.Models;
using LaNata_Api.Core.Models.Constants;
using LaNata_Api.Core.Models.DTO;
using LaNata_Api.Core.Models.DTO.Expense;
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
    public class ExpenseService(APIContext context, AuditService auditService, ExportService exportService, ReportService reportService, TokenService tokenService)
    {
        private readonly APIContext _db = context;
        private readonly AuditService _auditService = auditService;
        private readonly ExportService _exportService = exportService;
        private readonly ReportService _reportService = reportService;
        private readonly TokenService _tokenService = tokenService;

        #region CRUD
        public async Task<BaseResponse<GetAllResponse>> GetAll(GetAllRequest rq)
        {
            rq ??= new GetAllRequest();

            var rs = new BaseResponse<GetAllResponse>();

            var validationError = ServiceRequestValidator.ValidateSort(rq)
                ?? ServiceRequestValidator.ValidateSearch(rq.Search);
            if (validationError != null)
                return rs.SetError(validationError);

            if (rq.DateFrom.HasValue && rq.DateTo.HasValue && rq.DateFrom > rq.DateTo)
                return rs.SetError(Messages.Error.InvalidDateRange());

            var query = _db.Expense.AsQueryable();

            query = FilterQuery(query, rq);
            query = OrderQuery(query, rq);

            rs.Data = new GetAllResponse
            {
                Items = await query
                    .Select(x => new GetAllResponse.Item
                    {
                        Id = x.Id,
                        Date = x.Date,
                        Description = x.Description,
                        Price = x.Price
                    })
                    .ToListAsync()
            };

            return rs;
        }

        public async Task<BaseResponse<GetOneResponse>> GetOne(GetOneRequest rq)
        {
            var rs = new BaseResponse<GetOneResponse>();

            if (rq == null || rq.Id <= 0)
                return rs.SetError(Messages.Error.FieldGraterThanZero("id"));

            var expense = await _db
                .Expense
                .Where(x => x.Id == rq.Id)
                .Select(x => new GetOneResponse
                {
                    Id = x.Id,
                    Date = x.Date,
                    Description = x.Description,
                    Price = x.Price,
                    CreatedAt = x.CreatedAt
                })
                .AsNoTracking()
                .FirstOrDefaultAsync();

            if (expense == null)
                return rs.SetError(Messages.Error.EntityNotFound("Gasto"));

            rs.Data = expense;
            return rs;
        }

        public async Task<BaseResponse<CreateResponse>> Create(CreateRequest rq)
        {
            var rs = new BaseResponse<CreateResponse>();

            if (rq == null)
                return rs.SetError(Messages.Error.FieldsRequired());

            var expense = new Expense
            {
                Date = rq.Date.Date,
                Description = rq.Description?.Trim(),
                Price = rq.Price
            };

            if (!rs.Attach(ValidateExpense<CreateResponse>(expense)).Success)
                return rs;

            try
            {
                _db.Expense.Add(expense);
                await _db.SaveChangesAsync();
            }
            catch (Exception)
            {
                return rs.SetError(Messages.Error.Exception());
            }

            await Audit(AuditActionCode.CREATED, expense, BuildAuditChanges(AuditChangeList.ForCreated(expense)));

            rs.Message = Messages.CRUD.EntityCreated("Gasto");
            rs.Data = new CreateResponse
            {
                Id = expense.Id,
                Date = expense.Date,
                Description = expense.Description,
                Price = expense.Price
            };

            return rs;
        }

        public async Task<BaseResponse> Update(UpdateRequest rq)
        {
            var rs = new BaseResponse();

            if (rq == null || rq.Id <= 0)
                return rs.SetError(Messages.Error.FieldGraterThanZero("id"));

            var expense = await _db.Expense.FirstOrDefaultAsync(x => x.Id == rq.Id);
            if (expense == null)
                return rs.SetError(Messages.Error.EntityNotFound("Gasto"));

            var previous = expense.Clone();

            expense.Date = rq.Date.Date;
            expense.Description = rq.Description?.Trim();
            expense.Price = rq.Price;
            expense.UpdatedAt = DateTime.UtcNow;

            var validationResponse = ValidateExpense<object>(expense);
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

            await Audit(AuditActionCode.UPDATED, expense, BuildAuditChanges(AuditChangeList.For(previous, expense)));

            rs.Message = Messages.CRUD.EntityUpdated("Gasto");
            return rs;
        }

        public async Task<BaseResponse> Delete(DeleteRequest rq)
        {
            var rs = new BaseResponse();

            if (rq == null || rq.Id <= 0)
                return rs.SetError(Messages.Error.FieldGraterThanZero("id"));

            var expense = await _db.Expense.FirstOrDefaultAsync(x => x.Id == rq.Id);
            if (expense == null)
                return rs.SetError(Messages.Error.EntityNotFound("Gasto"));

            expense.DeletedAt = DateTime.UtcNow;

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (Exception)
            {
                return rs.SetError(Messages.Error.Exception());
            }

            await Audit(AuditActionCode.DELETED, expense, BuildAuditChanges(AuditChangeList.ForDeleted(expense)));

            rs.Message = Messages.CRUD.EntityDeleted("Gasto");
            return rs;
        }
        #endregion

        #region Methods
        // Expenses against the income the orders produced in the same window, and the resulting
        // profit: the numbers the legacy balance screen showed
        public async Task<BaseResponse<GetBalanceResponse>> GetBalance(GetBalanceRequest rq)
        {
            var rs = new BaseResponse<GetBalanceResponse>();

            var dataResponse = await BuildBalance(rq);
            if (!dataResponse.Success)
                return rs.SetError(dataResponse.Error.Message);

            var data = dataResponse.Data;

            rs.Data = new GetBalanceResponse
            {
                DateFrom = data.DateFrom,
                DateTo = data.DateTo,
                TotalExpenses = data.TotalExpenses,
                TotalIncome = data.TotalIncome,
                Profit = data.Profit,
                Expenses = [.. data.Expenses.Select(x => new GetBalanceResponse.ExpenseItem
                {
                    Id = x.Id,
                    Date = x.Date,
                    Description = x.Description,
                    Price = x.Price
                })],
                Income = [.. data.Income.Select(x => new GetBalanceResponse.IncomeItem
                {
                    Id = x.Id,
                    Date = x.Date,
                    ClientName = x.ClientName,
                    EventAddress = x.EventAddress,
                    ShipmentPrice = x.ShipmentPrice,
                    Total = x.Total
                })]
            };

            return rs;
        }
        #endregion

        #region Documents
        public async Task<BaseResponse<ExportFile>> Export(ExportFileFormat format, GetAllRequest rq)
        {
            var rs = new BaseResponse<ExportFile>();

            var expenses = await GetExpensesForDocument(rq);
            if (expenses.Count == 0)
                return rs.SetError(ExportMessages.NoRows());

            var columns = ExportColumns
                .For<Expense>()
                .Add(x => x.Id, "Id", ExportValueFormat.Integer)
                .Add(x => x.Date, "Fecha", ExportValueFormat.Date)
                .Add(x => x.Description, "Descripción", width: 45)
                .Add(x => x.Price, "Monto", ExportValueFormat.Currency)
                .Build();

            rs = await _exportService.Export(format, new ExportRequest<Expense>
            {
                FileName = "gastos",
                SheetName = "Gastos",
                Columns = columns,
                Rows = expenses
            });

            if (rs.Success)
                await AuditDocument(AuditActionCode.EXPORTED, rs.Data.FileName, rs.Data.RowCount, format.ToString(), BuildReportFilters(rq));

            return rs;
        }

        public async Task<BaseResponse<ReportFile>> Report(GetBalanceRequest rq)
        {
            var rs = new BaseResponse<ReportFile>();

            var dataResponse = await BuildBalance(rq);
            if (!dataResponse.Success)
                return rs.SetError(dataResponse.Error.Message);

            var data = dataResponse.Data;

            if (data.Expenses.Count == 0 && data.Income.Count == 0)
                return rs.SetError(ReportMessages.NoRows());

            var options = new ReportOptions
            {
                Title = CompanyInfo.Name,
                Subtitle = CompanyInfo.Slogan + Environment.NewLine + CompanyInfo.Tagline,
                DocumentTitle = "DETALLE DE GASTOS E INGRESOS",
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
                Filters =
                [
                    new("Desde", data.DateFrom.ToString("dd/MM/yyyy")),
                    new("Hasta", data.DateTo.ToString("dd/MM/yyyy"))
                ],
                Summary =
                [
                    new("Gastos", Currency(data.TotalExpenses)),
                    new("Ingresos", Currency(data.TotalIncome)),
                    new("Ganancias", Currency(data.Profit))
                ]
            };

            rs = _reportService.Generate(new BalanceReportDocument(data, options), "balance");

            if (rs.Success)
            {
                await AuditDocument(AuditActionCode.REPORT_GENERATED, rs.Data.FileName, data.Expenses.Count + data.Income.Count, "Pdf",
                [
                    new("Desde", data.DateFrom.ToString("dd/MM/yyyy")),
                    new("Hasta", data.DateTo.ToString("dd/MM/yyyy"))
                ]);
            }

            return rs;
        }
        #endregion

        #region Audit
        private static AuditChangeSet BuildAuditChanges(AuditChangeList<Expense> changeList)
        {
            return changeList
                .AddField(x => x.Date, "Fecha", AuditFormat.Date)
                .AddField(x => x.Description, "Descripción")
                .AddField(x => x.Price, "Monto", AuditFormat.Currency)
                .Build();
        }

        private async Task Audit(string actionCode, Expense expense, AuditChangeSet changes)
        {
            await _auditService.Audit(new AuditRequest
            {
                ActionCode = actionCode,
                Entity = AuditEntityCode.EXPENSE,
                EntityKey = expense.Id.ToString(),
                Name = expense.Description,
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
                Entity = AuditEntityCode.EXPENSE,
                Name = fileName,
                Description = "Se generó " + fileName + " con " + rowCount + " registro(s)",
                Payload = payload
            });
        }
        #endregion

        #region Validations
        private static BaseResponse<T> ValidateExpense<T>(Expense expense)
        {
            var rs = new BaseResponse<T>();

            if (expense.Date == default)
                return rs.SetError(Messages.Error.FieldRequired("fecha"));

            if (string.IsNullOrWhiteSpace(expense.Description))
                return rs.SetError(Messages.Error.FieldRequired("descripción"));

            if (expense.Description.Length > ValidationLimits.ExpenseDescriptionMaxLength)
                return rs.SetError(Messages.Error.FieldTooLong("descripción", ValidationLimits.ExpenseDescriptionMaxLength));

            if (expense.Price <= 0 || expense.Price > ValidationLimits.ExpensePriceMax)
                return rs.SetError(Messages.Error.FieldGraterThanZero("monto"));

            return rs;
        }
        #endregion

        #region Helpers
        private async Task<BaseResponse<BalanceReportData>> BuildBalance(GetBalanceRequest rq)
        {
            var rs = new BaseResponse<BalanceReportData>();

            if (rq == null || rq.DateFrom == default || rq.DateTo == default)
                return rs.SetError(Messages.Error.FieldsRequired(["Desde", "Hasta"]));

            var dateFrom = rq.DateFrom.Date;
            var dateTo = rq.DateTo.Date;

            if (dateFrom > dateTo)
                return rs.SetError(Messages.Error.InvalidDateRange());

            var rangeEnd = dateTo.AddDays(1);

            var expenses = await _db
                .Expense
                .AsNoTracking()
                .Where(x => x.Date >= dateFrom && x.Date < rangeEnd)
                .OrderByDescending(x => x.Date)
                .ThenByDescending(x => x.Price)
                .Select(x => new BalanceExpenseRow
                {
                    Id = x.Id,
                    Date = x.Date,
                    Description = x.Description,
                    Price = x.Price
                })
                .ToListAsync();

            var income = await _db
                .Order
                .AsNoTracking()
                .Where(x => x.Date >= dateFrom && x.Date < rangeEnd)
                .Select(x => new BalanceIncomeRow
                {
                    Id = x.Id,
                    Date = x.Date,
                    ClientName = x.ClientName,
                    EventAddress = x.EventAddress,
                    ShipmentPrice = x.ShipmentPrice,
                    Total = x.Products.Sum(p => p.Quantity * p.UnitPrice) + (x.ShipmentPrice ?? 0)
                })
                .OrderByDescending(x => x.Date)
                .ThenByDescending(x => x.Total)
                .ToListAsync();

            rs.Data = new BalanceReportData
            {
                DateFrom = dateFrom,
                DateTo = dateTo,
                Expenses = expenses,
                Income = income,
                TotalExpenses = expenses.Sum(x => x.Price),
                TotalIncome = income.Sum(x => x.Total)
            };

            return rs;
        }

        private async Task<List<Expense>> GetExpensesForDocument(GetAllRequest rq)
        {
            rq ??= new GetAllRequest();

            var query = _db.Expense.AsNoTracking().AsQueryable();

            query = FilterQuery(query, rq);
            query = OrderQuery(query, rq);

            return await query.ToListAsync();
        }

        private static string Currency(decimal value)
        {
            return value.ToString("C2", ReportCulture.Current);
        }

        private static List<ReportField> BuildReportFilters(GetAllRequest rq)
        {
            var filters = new List<ReportField>();

            if (rq == null)
                return filters;

            if (rq.DateFrom.HasValue)
                filters.Add(new ReportField("Desde", rq.DateFrom.Value.ToString("dd/MM/yyyy")));

            if (rq.DateTo.HasValue)
                filters.Add(new ReportField("Hasta", rq.DateTo.Value.ToString("dd/MM/yyyy")));

            if (!string.IsNullOrWhiteSpace(rq.Search))
                filters.Add(new ReportField("Búsqueda", rq.Search));

            return filters;
        }

        private static IQueryable<Expense> FilterQuery(IQueryable<Expense> query, GetAllRequest rq)
        {
            if (rq.DateFrom.HasValue)
            {
                var dateFrom = rq.DateFrom.Value.Date;
                query = query.Where(x => x.Date >= dateFrom);
            }

            // Half-open range: stored dates may carry a time component
            if (rq.DateTo.HasValue)
            {
                var dateTo = rq.DateTo.Value.Date.AddDays(1);
                query = query.Where(x => x.Date < dateTo);
            }

            if (!string.IsNullOrWhiteSpace(rq.Search))
            {
                var search = rq.Search.Trim();
                query = query.Where(x => EF.Functions.Like(x.Description, "%" + search + "%"));
            }

            return query;
        }

        private static IQueryable<Expense> OrderQuery(IQueryable<Expense> query, SortRequest rq)
        {
            return rq.ColumnSort switch
            {
                "description" => rq.SortDirection == SortDirectionCode.ASC ? query.OrderBy(x => x.Description) : query.OrderByDescending(x => x.Description),
                "price" => rq.SortDirection == SortDirectionCode.ASC ? query.OrderBy(x => x.Price) : query.OrderByDescending(x => x.Price),
                "date" => rq.SortDirection == SortDirectionCode.ASC ? query.OrderBy(x => x.Date) : query.OrderByDescending(x => x.Date),
                _ => query.OrderByDescending(x => x.Date).ThenByDescending(x => x.Price),
            };
        }
        #endregion
    }
}
