using System;
using System.ComponentModel.DataAnnotations.Schema;
using LaNata_Api.Core.Models;

namespace LaNata_Api.Audit.Models
{
    public class AuditableEntity
    {
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
        public int? CreatedBy { get; set; }
        public int? UpdatedBy { get; set; }
        public int? DeletedBy { get; set; }

        [ForeignKey("CreatedBy")]
        public virtual User CreatedByUser { get; set; }

        [ForeignKey("UpdatedBy")]
        public virtual User UpdatedByUser { get; set; }

        [ForeignKey("DeletedBy")]
        public virtual User DeletedByUser { get; set; }
    }
}
