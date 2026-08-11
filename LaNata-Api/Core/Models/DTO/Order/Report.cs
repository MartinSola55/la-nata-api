using System;
using System.Collections.Generic;

namespace LaNata_Api.Core.Models.DTO.Order
{
    // The legacy app printed the same order two ways: with prices for the client, without them for
    // the delivery sheet
    public class ReportRequest
    {
        public int Id { get; set; }
        public bool WithPrice { get; set; } = true;
    }

    // Budget for an order that does not exist yet, so the client can be quoted before confirming
    public class BudgetRequest
    {
        public string ClientName { get; set; }
        public string EventAddress { get; set; }
        public DateTime Date { get; set; }
        public double? ShipmentPrice { get; set; }
        public List<OrderProductRequest> Products { get; set; } = [];
    }
}
