using System;

namespace LaNata_Api.Core.Models.DTO.Expense
{
    public class CreateRequest
    {
        public DateTime Date { get; set; }
        public string Description { get; set; }
        public double Price { get; set; }
    }

    public class CreateResponse
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string Description { get; set; }
        public double Price { get; set; }
    }
}
