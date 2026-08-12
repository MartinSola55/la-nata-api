using System;
using System.Collections.Generic;

namespace LaNata_Api.Core.Models.DTO.Expense
{
    public class GetAllRequest : SortRequest
    {
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public string Search { get; set; }
    }

    public class GetAllResponse
    {
        public List<Item> Items { get; set; } = [];

        public class Item
        {
            public int Id { get; set; }
            public DateTime Date { get; set; }
            public string Description { get; set; }
            public decimal Price { get; set; }
        }
    }
}
