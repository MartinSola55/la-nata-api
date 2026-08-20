using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LaNata_Api.Core.Models
{
    // SQL Server treats NULLs as equal inside a unique index, which is exactly what keeps an order to
    // a single "sin variante" line per product and price. Do not "fix" that into a filtered index.
    [Table("ProductOrder")]
    [Index(nameof(ProductId), nameof(OrderId), nameof(UnitPrice), nameof(VariantId), IsUnique = true)]
    public class ProductOrder
    {
        [Key]
        public int Id { get; set; }
        public int ProductId { get; set; }

        public int OrderId { get; set; }
        public int Quantity { get; set; }

        [Precision(18, 2)]
        public decimal UnitPrice { get; set; }

        public int? VariantId { get; set; }

        [ForeignKey("OrderId")]
        public virtual Order Order { get; set; }

        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; }

        [ForeignKey("VariantId")]
        public virtual ProductVariant Variant { get; set; }
    }
}
