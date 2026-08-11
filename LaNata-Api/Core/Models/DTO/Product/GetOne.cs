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
        public long Stock { get; set; }
        public double Price { get; set; }
        public double BreakPrice { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
