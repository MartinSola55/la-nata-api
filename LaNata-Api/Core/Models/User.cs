using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LaNata_Api.Audit.Models;

namespace LaNata_Api.Core.Models
{
    [Table("User")]
    public class User : AuditableEntity
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("id_rol")]
        public int RoleId { get; set; }

        [Column("email")]
        public string Email { get; set; }

        [Column("password")]
        public string PasswordHash { get; set; }

        [ForeignKey("RoleId")]
        public virtual Role Role { get; set; }
    }
}
