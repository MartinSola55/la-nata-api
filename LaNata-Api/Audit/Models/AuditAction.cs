using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LaNata_Api.Audit.Models
{
    [Table("AuditAction", Schema = "audit")]
    public class AuditAction
    {
        [Key]
        public string ActionCode { get; set; }
        public string ActionName { get; set; }
        public string CategoryCode { get; set; }
        public bool IsInternal { get; set; }

        [ForeignKey("CategoryCode")]
        public virtual AuditCategory Category { get; set; }
    }
}
