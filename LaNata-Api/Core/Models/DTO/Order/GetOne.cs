using System;
using System.Collections.Generic;

namespace LaNata_Api.Core.Models.DTO.Order
{
    public class GetOneRequest
    {
        public int Id { get; set; }
    }

    public class GetOneResponse
    {
        public int Id { get; set; }
        public string ClientName { get; set; }
        public string Phone { get; set; }
        public string EventAddress { get; set; }
        public DateTime Date { get; set; }
        public double? ShipmentPrice { get; set; }
        public string Observation { get; set; }
        public double ProductsTotal { get; set; }
        public double Total { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<OrderProductResponse> Products { get; set; } = [];
    }

    // There is no surrogate id: a line is identified by product + unit price, which is also what
    // AddProduct and RemoveProduct take
    public class OrderProductResponse
    {
        public int ProductId { get; set; }
        public string Description { get; set; }
        public int Quantity { get; set; }
        public double UnitPrice { get; set; }
        public double BreakPrice { get; set; }
        public double Subtotal { get; set; }
    }
}
