using System;
using System.Collections.Generic;

namespace LaNata_Api.Core.Models.DTO.Order
{
    public class CreateRequest
    {
        public string ClientName { get; set; }
        public string Phone { get; set; }
        public string EventAddress { get; set; }
        public DateTime Date { get; set; }
        public decimal? ShipmentPrice { get; set; }
        public string Observation { get; set; }
        public bool IsDraft { get; set; }

        public List<OrderProductRequest> Products { get; set; } = [];
    }

    public class OrderProductRequest
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal? UnitPrice { get; set; }
        public int? VariantId { get; set; }
    }

    public class CreateResponse
    {
        public int Id { get; set; }
        public string ClientName { get; set; }
        public DateTime Date { get; set; }
        public decimal Total { get; set; }
        public bool IsDraft { get; set; }
        public List<OrderProductResponse> Products { get; set; } = [];
    }
}
