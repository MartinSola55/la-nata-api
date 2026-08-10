using System.ComponentModel.DataAnnotations;
using Template_Api.Audit.Models;

namespace Template_Api.Core.Models
{
    public class Product : AuditableEntity
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public string PictureUrl { get; set; }
        public string CategoryId { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
