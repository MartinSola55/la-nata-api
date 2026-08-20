using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LaNata_Api.Audit.Models;

namespace LaNata_Api.Core.Models
{
    [Table("Category")]
    public class Category : AuditableEntity
    {
        [Key]
        public int Id { get; set; }
        public string Description { get; set; }

        public virtual List<Product> Products { get; set; } = [];
    }
}
