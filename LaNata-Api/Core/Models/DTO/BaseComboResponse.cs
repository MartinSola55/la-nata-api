using System.Collections.Generic;

namespace LaNata_Api.Core.Models.DTO
{
    public class BaseComboResponse
    {
        public List<Item> Items { get; set; } = [];

        public class Item
        {
            public int Id { get; set; }
            public string Description { get; set; }
        }
    }
}
