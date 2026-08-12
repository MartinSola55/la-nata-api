using System;
using System.Collections.Generic;

namespace LaNata_Api.Core.Models.DTO.Order
{
    public class GetAllRequest : SortRequest
    {
        // The legacy screen is "the orders of one day"; the range is what the balance report needs
        public DateTime? Date { get; set; }
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
            public string ClientName { get; set; }
            public string Phone { get; set; }
            public string EventAddress { get; set; }
            public DateTime Date { get; set; }
            public decimal? ShipmentPrice { get; set; }
            public string Observation { get; set; }
            public int ProductsCount { get; set; }
            public decimal Total { get; set; }
            public DateTime CreatedAt { get; set; }
        }
    }
}
