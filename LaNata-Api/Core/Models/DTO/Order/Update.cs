using System;
using System.Collections.Generic;

namespace LaNata_Api.Core.Models.DTO.Order
{
    public class UpdateRequest
    {
        public int Id { get; set; }
        public string ClientName { get; set; }
        public string Phone { get; set; }
        public string EventAddress { get; set; }
        public DateTime Date { get; set; }
        public decimal? ShipmentPrice { get; set; }
        public string Observation { get; set; }
        public List<OrderProductRequest> Products { get; set; } = [];
    }
}
