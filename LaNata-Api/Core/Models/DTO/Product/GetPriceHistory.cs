using System;
using System.Collections.Generic;

namespace LaNata_Api.Core.Models.DTO.Product
{
    public class GetPriceHistoryRequest
    {
        public int Id { get; set; }
    }

    public class GetPriceHistoryResponse
    {
        public List<Item> Items { get; set; } = [];

        public class Item
        {
            public int Id { get; set; }
            public decimal PreviousPrice { get; set; }
            public decimal NewPrice { get; set; }
            public decimal? PreviousBreakPrice { get; set; }
            public decimal? NewBreakPrice { get; set; }
            public DateTime ChangedAt { get; set; }
            public string ChangedByEmail { get; set; }
            public int RepricedOrders { get; set; }
            public int RepricedLines { get; set; }
            public int SkippedLines { get; set; }
        }
    }
}
