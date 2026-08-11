using System;
using System.ComponentModel.DataAnnotations.Schema;
using LaNata_Api.Core.Models;

namespace LaNata_Api.Audit.Models
{
    // Columns are snake_case because this maps onto the legacy production schema, which already
    // used created_at / deleted_at. The *_by columns are the only thing the migration adds.
    public class AuditableEntity
    {
        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [Column("deleted_at")]
        public DateTime? DeletedAt { get; set; }

        [Column("created_by")]
        public int? CreatedBy { get; set; }

        [Column("updated_by")]
        public int? UpdatedBy { get; set; }

        [Column("deleted_by")]
        public int? DeletedBy { get; set; }

        [ForeignKey("CreatedBy")]
        public virtual User CreatedByUser { get; set; }

        [ForeignKey("UpdatedBy")]
        public virtual User UpdatedByUser { get; set; }

        [ForeignKey("DeletedBy")]
        public virtual User DeletedByUser { get; set; }
    }
}
