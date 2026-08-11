using System;
using System.Collections.Generic;

namespace LaNata_Api.Core.Models.Internal
{
    // Computed once by ExpenseService and reused by both the JSON balance and the PDF document
    public class BalanceReportData
    {
        public DateTime DateFrom { get; set; }
        public DateTime DateTo { get; set; }
        public double TotalExpenses { get; set; }
        public double TotalIncome { get; set; }
        public double Profit => TotalIncome - TotalExpenses;
        public List<BalanceExpenseRow> Expenses { get; set; } = [];
        public List<BalanceIncomeRow> Income { get; set; } = [];
    }

    public class BalanceExpenseRow
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string Description { get; set; }
        public double Price { get; set; }
    }

    public class BalanceIncomeRow
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string ClientName { get; set; }
        public string EventAddress { get; set; }
        public double? ShipmentPrice { get; set; }
        public double Total { get; set; }
    }
}
