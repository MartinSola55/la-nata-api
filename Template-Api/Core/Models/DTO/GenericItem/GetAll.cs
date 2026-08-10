using System;
using System.Collections.Generic;

namespace Template_Api.Core.Models.DTO.GenericItem
{
    public class GetAllRequest : PaginateRequest
    {
        public string Search { get; set; }
        public bool? IsActive { get; set; }
    }

    public class GetAllResponse
    {
        public List<Item> Items { get; set; } = [];

        public class Item
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public bool IsActive { get; set; }
            public DateTime CreatedAt { get; set; }
        }
    }
}
