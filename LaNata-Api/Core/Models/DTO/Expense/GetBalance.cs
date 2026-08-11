using System;
using System.Collections.Generic;

namespace LaNata_Api.Core.Models.DTO.Expense
{
    // Expenses, income and profit between two dates: the data behind the legacy balance report
    public class GetBalanceRequest
    {
        public DateTime DateFrom { get; set; }
        public DateTime DateTo { get; set; }
    }

    public class GetBalanceResponse
    {
        public DateTime DateFrom { get; set; }
        public DateTime DateTo { get; set; }
        public double TotalExpenses { get; set; }
        public double TotalIncome { get; set; }
        public double Profit { get; set; }
        public List<ExpenseItem> Expenses { get; set; } = [];
        public List<IncomeItem> Income { get; set; } = [];

        public class ExpenseItem
        {
            public int Id { get; set; }
            public DateTime Date { get; set; }
            public string Description { get; set; }
            public double Price { get; set; }
        }

        public class IncomeItem
        {
            public int Id { get; set; }
            public DateTime Date { get; set; }
            public string ClientName { get; set; }
            public string EventAddress { get; set; }
            public double? ShipmentPrice { get; set; }
            public double Total { get; set; }
        }
    }
}
