using System.ComponentModel.DataAnnotations;
using Template_Api.Audit.Models;

namespace Template_Api.Core.Models
{
    public class GenericItem : AuditableEntity
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
