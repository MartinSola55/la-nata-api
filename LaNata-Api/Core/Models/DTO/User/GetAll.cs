using System;
using System.Collections.Generic;

namespace LaNata_Api.Core.Models.DTO.User
{
    public class GetAllRequest : PaginateRequest
    {
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public List<int> Roles { get; set; } = [];
    }

    public class GetAllResponse
    {
        public List<Item> Items { get; set; } = [];

        public class Item
        {
            public int Id { get; set; }
            public string Role { get; set; }
            public string Email { get; set; }
            public DateTime CreatedAt { get; set; }
        }
    }
}
