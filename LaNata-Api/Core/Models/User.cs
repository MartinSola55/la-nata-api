using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LaNata_Api.Audit.Models;

namespace LaNata_Api.Core.Models
{
    [Table("User")]
    public class User : AuditableEntity
    {
        [Key]
        public int Id { get; set; }
        public int RoleId { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }

        [ForeignKey("RoleId")]
        public virtual Role Role { get; set; }
    }
}
