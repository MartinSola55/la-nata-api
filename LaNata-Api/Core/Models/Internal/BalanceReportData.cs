using System;
using System.Collections.Generic;

namespace LaNata_Api.Core.Models.Internal
{
    // Computed once by ExpenseService and reused by both the JSON balance and the PDF document
    public class BalanceReportData
    {
        public DateTime DateFrom { get; set; }
        public DateTime DateTo { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal TotalIncome { get; set; }
        public decimal TotalIncomePaid { get; set; }
        public decimal Profit => TotalIncome - TotalExpenses;
        public List<BalanceExpenseRow> Expenses { get; set; } = [];
        public List<BalanceIncomeRow> Income { get; set; } = [];
    }

    public class BalanceExpenseRow
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
    }

    public class BalanceIncomeRow
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string ClientName { get; set; }
        public string EventAddress { get; set; }
        public decimal? ShipmentPrice { get; set; }
        public decimal Total { get; set; }
        public DateTime? PaidAt { get; set; }
    }
}
