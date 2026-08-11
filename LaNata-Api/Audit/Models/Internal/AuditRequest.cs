using System;

namespace LaNata_Api.Audit.Models.Internal
{
    public class AuditRequest
    {
        // Must exist in audit.AuditAction, so a new code needs a seed migration
        public string ActionCode { get; set; }
        public string Entity { get; set; }
        public string EntityKey { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime? Date { get; set; }
        public AuditChangeSet Changes { get; set; }

        // Explicit snapshots override the ones coming from Changes
        public object State { get; set; }
        public object Payload { get; set; }
    }
}
