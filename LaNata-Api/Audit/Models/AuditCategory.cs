using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LaNata_Api.Audit.Models
{
    [Table("AuditCategory", Schema = "audit")]
    public class AuditCategory
    {
        [Key]
        public string CategoryCode { get; set; }
        public string CategoryName { get; set; }
        public bool IsInternal { get; set; }
    }
}
