using System;

namespace LaNata_Api.Core.Models.DTO.Expense
{
    public class UpdateRequest
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string Description { get; set; }
        public double Price { get; set; }
    }
}
