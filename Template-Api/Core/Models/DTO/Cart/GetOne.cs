using System;
using System.Collections.Generic;

namespace Template_Api.Core.Models.DTO.Cart
{
    public class GetOneRequest
    {
        public int Id { get; set; }
    }

    public class GetOneResponse
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string UserEmail { get; set; }
        public string Status { get; set; }
        public decimal Total { get; set; }
        public DateTime? PaidAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<CartProductResponse> Products { get; set; } = [];
    }
}
