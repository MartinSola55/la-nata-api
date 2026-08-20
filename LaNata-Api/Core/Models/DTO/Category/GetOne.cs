using System;

namespace LaNata_Api.Core.Models.DTO.Category
{
    public class GetOneRequest
    {
        public int Id { get; set; }
    }

    public class GetOneResponse
    {
        public int Id { get; set; }
        public string Description { get; set; }
        public int ProductsCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
