using System.Collections.Generic;
using System.Linq;

namespace Template_Api.Audit.Models.Internal
{
    public class AuditChangeSet
    {
        // Snapshots only carry the fields registered in the change list, never the whole entity
        public List<AuditChange> Changes { get; set; } = [];
        public Dictionary<string, object> State { get; set; }
        public Dictionary<string, object> Payload { get; set; }

        public bool HasChanges => Changes.Count > 0;
        public List<string> Descriptions => [.. Changes.Select(x => x.Description)];
    }
}
