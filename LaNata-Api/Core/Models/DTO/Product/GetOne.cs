using System;

namespace LaNata_Api.Core.Models.DTO.Product
{
    public class GetOneRequest
    {
        public int Id { get; set; }
    }

    public class GetOneResponse
    {
        public int Id { get; set; }
        public string Description { get; set; }
        public int Stock { get; set; }
        public decimal Price { get; set; }
        public decimal? BreakPrice { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
