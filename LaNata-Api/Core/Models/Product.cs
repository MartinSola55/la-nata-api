using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LaNata_Api.Audit.Models;

namespace LaNata_Api.Core.Models
{
    [Table("Product")]
    public class Product : AuditableEntity
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("description")]
        public string Description { get; set; }

        [Column("stock")]
        public long Stock { get; set; }

        // float in the legacy schema. Money would normally be decimal, but the column type is not
        // ours to change, and mapping decimal onto float would silently round on every write.
        [Column("price")]
        public double Price { get; set; }

        [Column("break_price")]
        public double BreakPrice { get; set; }

        public virtual List<ProductOrder> Orders { get; set; } = [];
    }
}
