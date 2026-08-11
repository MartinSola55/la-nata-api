using System.Collections.Generic;
using LaNata_Api.Audit.Models.Internal;

namespace LaNata_Api.Audit.Helpers.ChangeList
{
    internal abstract class AuditItemBase
    {
        public string Field { get; set; }
        public string FieldName { get; set; }

        public abstract List<AuditChange> Process(object previous, object updated);

        public abstract KeyValuePair<string, object> Snapshot(object instance);
    }
}
