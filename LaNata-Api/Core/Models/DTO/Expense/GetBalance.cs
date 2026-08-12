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
        public decimal TotalExpenses { get; set; }
        public decimal TotalIncome { get; set; }
        public decimal Profit { get; set; }
        public List<ExpenseItem> Expenses { get; set; } = [];
        public List<IncomeItem> Income { get; set; } = [];

        public class ExpenseItem
        {
            public int Id { get; set; }
            public DateTime Date { get; set; }
            public string Description { get; set; }
            public decimal Price { get; set; }
        }

        public class IncomeItem
        {
            public int Id { get; set; }
            public DateTime Date { get; set; }
            public string ClientName { get; set; }
            public string EventAddress { get; set; }
            public decimal? ShipmentPrice { get; set; }
            public decimal Total { get; set; }
        }
    }
}
