using System;
using System.Collections.Generic;

namespace Template_Api.Core.Models.DTO.Cart
{
    public class GetAllRequest : PaginateRequest
    {
        public string Status { get; set; }
        public int? UserId { get; set; }
    }

    public class GetAllResponse
    {
        public List<Item> Items { get; set; } = [];

        public class Item
        {
            public int Id { get; set; }
            public int UserId { get; set; }
            public string UserEmail { get; set; }
            public string Status { get; set; }
            public decimal Total { get; set; }
            public int ProductsCount { get; set; }
            public DateTime? PaidAt { get; set; }
            public DateTime CreatedAt { get; set; }
        }
    }
}
