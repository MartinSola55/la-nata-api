using System.Collections.Generic;

namespace LaNata_Api.Audit.Models.DTO
{
    public class GetFiltersResponse
    {
        public List<CategoryItem> Categories { get; set; } = [];
        public List<ActionItem> Actions { get; set; } = [];

        public class CategoryItem
        {
            public string CategoryCode { get; set; }
            public string CategoryName { get; set; }
        }

        public class ActionItem
        {
            public string ActionCode { get; set; }
            public string ActionName { get; set; }
            public string CategoryCode { get; set; }
        }
    }
}
