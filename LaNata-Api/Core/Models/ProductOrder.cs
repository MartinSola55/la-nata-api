using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace LaNata_Api.Core.Models
{
    // Keyed by (product, order, price) so one order can reserve the same product twice at different
    // prices. This is a join table with no audit columns: the legacy hard deleted its rows and the
    // production schema has no deleted_at here, so RemoveProduct hard deletes too.
    [Table("ProductOrder")]
    [PrimaryKey(nameof(ProductId), nameof(OrderId), nameof(UnitPrice))]
    public class ProductOrder
    {
        [Column("id_product")]
        public int ProductId { get; set; }

        [Column("id_order")]
        public int OrderId { get; set; }

        [Column("quantity")]
        public int Quantity { get; set; }

        [Column("unit_price")]
        public double UnitPrice { get; set; }

        [ForeignKey("OrderId")]
        public virtual Order Order { get; set; }

        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; }
    }
}
