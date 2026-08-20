using System;
using System.Collections.Generic;

namespace LaNata_Api.Core.Models.DTO.Category
{
    public class GetAllRequest : SortRequest
    {
        public string Search { get; set; }
    }

    public class GetAllResponse
    {
        public List<Item> Items { get; set; } = [];

        public class Item
        {
            public int Id { get; set; }
            public string Description { get; set; }
            public int ProductsCount { get; set; }
            public DateTime CreatedAt { get; set; }
        }
    }
}
