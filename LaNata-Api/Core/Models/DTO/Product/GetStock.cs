using System;
using System.Collections.Generic;

namespace LaNata_Api.Core.Models.DTO.Product
{
    // Availability for a given event date: the catalogue stock minus everything already reserved by
    // the orders of that day. This is what the order screens ask before letting a product be added.
    public class GetStockRequest
    {
        public string Search { get; set; }
        public DateTime Date { get; set; }
        public int? ExcludeOrderId { get; set; }
    }

    public class GetStockResponse
    {
        public List<Item> Items { get; set; } = [];

        public class Item
        {
            public int Id { get; set; }
            public string Description { get; set; }
            public double Price { get; set; }
            public double BreakPrice { get; set; }
            public long Stock { get; set; }
            public long AvailableStock { get; set; }
            public long ReservedStock { get; set; }
        }
    }
}
