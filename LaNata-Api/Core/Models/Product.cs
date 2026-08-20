using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LaNata_Api.Audit.Models;
using Microsoft.EntityFrameworkCore;

namespace LaNata_Api.Core.Models
{
    [Table("Product")]
    public class Product : AuditableEntity
    {
        [Key]
        public int Id { get; set; }
        public string Description { get; set; }
        public int Stock { get; set; }

        [Precision(18, 2)]
        public decimal Price { get; set; }

        [Precision(18, 2)]
        public decimal? BreakPrice { get; set; }

        public int? CategoryId { get; set; }

        [ForeignKey("CategoryId")]
        public virtual Category Category { get; set; }

        public virtual List<ProductVariant> Variants { get; set; } = [];
        public virtual List<ProductOrder> Orders { get; set; } = [];
    }
}
