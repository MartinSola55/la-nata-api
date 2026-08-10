using System;
using System.Collections.Generic;

namespace Template_Api.Core.Models.DTO.Product
{
    public class GetAllRequest : PaginateRequest
    {
        public string Search { get; set; }
        public bool? IsActive { get; set; }
        public bool? HasStock { get; set; }
    }

    public class GetAllResponse
    {
        public List<Item> Items { get; set; } = [];

        public class Item
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public decimal Price { get; set; }
            public int Stock { get; set; }
            public string PictureUrl { get; set; }
            public string CategoryId { get; set; }
            public bool IsActive { get; set; }
            public DateTime CreatedAt { get; set; }
        }
    }
}
