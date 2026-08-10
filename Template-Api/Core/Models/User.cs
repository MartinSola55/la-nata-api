using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Template_Api.Audit.Models;

namespace Template_Api.Core.Models
{
    public class User : AuditableEntity
    {
        [Key]
        public int Id { get; set; }
        public int RoleId { get; set; }
        public string Name { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }

        [ForeignKey("RoleId")]
        public virtual Role Role { get; set; }
    }
}
