using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Template_Api.Audit.Models
{
    [Table("AuditEntry", Schema = "audit")]
    public class AuditEntry
    {
        [Key]
        public int Id { get; set; }
        public DateTime Date { get; set; } = DateTime.UtcNow;
        public string ActionCode { get; set; }
        public string Entity { get; set; }
        public string EntityKey { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }

        // Deliberately not a foreign key to dbo.User: an audit row must survive the user being
        // removed, and writing it must never take locks on operational tables
        public int? UserId { get; set; }
        public string UserName { get; set; }
        public string UserEmail { get; set; }
        public string IpAddress { get; set; }

        // JSON: before snapshot, after snapshot, and the change list
        public string State { get; set; }
        public string Payload { get; set; }
        public string Summary { get; set; }

        [ForeignKey("ActionCode")]
        public virtual AuditAction Action { get; set; }
    }
}
