using System;
using System.Collections.Generic;
using Template_Api.Core.Models.DTO;

namespace Template_Api.Audit.Models.DTO
{
    public class GetAllRequest : PaginateRequest
    {
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public List<string> ActionCodes { get; set; }
        public List<string> CategoryCodes { get; set; }
        public string Entity { get; set; }
        public string EntityKey { get; set; }
        public int? UserId { get; set; }
        public string Search { get; set; }

        public bool IncludeInternal { get; set; }
    }

    public class GetAllResponse
    {
        public List<Item> Items { get; set; } = [];

        public class Item
        {
            public int Id { get; set; }
            public DateTime Date { get; set; }
            public string ActionCode { get; set; }
            public string ActionName { get; set; }
            public string CategoryCode { get; set; }
            public string CategoryName { get; set; }
            public string Entity { get; set; }
            public string EntityKey { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public int? UserId { get; set; }
            public string UserName { get; set; }
            public string UserEmail { get; set; }
        }
    }
}
