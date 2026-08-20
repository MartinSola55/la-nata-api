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
            public string CategoryDescription { get; set; }
            public decimal Price { get; set; }
            public decimal? BreakPrice { get; set; }
            public int Stock { get; set; }
            public int AvailableStock { get; set; }
            public int ReservedStock { get; set; }

            public List<Variant> Variants { get; set; } = [];

            public class Variant
            {
                public int Id { get; set; }
                public string Description { get; set; }
                public decimal? PriceExtra { get; set; }
                public int Stock { get; set; }
                public int AvailableStock { get; set; }
                public int ReservedStock { get; set; }
            }
        }
    }
}
