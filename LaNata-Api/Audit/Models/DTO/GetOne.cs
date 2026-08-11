using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using LaNata_Api.Audit.Models.Internal;

namespace LaNata_Api.Audit.Models.DTO
{
    public class GetOneRequest
    {
        public int Id { get; set; }
    }

    public class GetOneResponse
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
        public string IpAddress { get; set; }
        public JsonNode State { get; set; }
        public JsonNode Payload { get; set; }
        public List<AuditChange> Summary { get; set; }
    }
}
