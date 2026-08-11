using System;
using System.Collections.Generic;

namespace LaNata_Api.Core.Models.DTO.Order
{
    // Feeds the calendar: the days that already have at least one order
    public class GetDatesRequest
    {
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
    }

    public class GetDatesResponse
    {
        public List<Item> Items { get; set; } = [];

        public class Item
        {
            public DateTime Date { get; set; }
            public int OrdersCount { get; set; }
        }
    }
}
