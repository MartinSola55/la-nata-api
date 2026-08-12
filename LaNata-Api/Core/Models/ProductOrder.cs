using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace LaNata_Api.Core.Models
{
    [Table("ProductOrder")]
    [PrimaryKey(nameof(ProductId), nameof(OrderId), nameof(UnitPrice))]
    public class ProductOrder
    {
        public int ProductId { get; set; }

        public int OrderId { get; set; }
        public int Quantity { get; set; }

        [Precision(18, 2)]
        public decimal UnitPrice { get; set; }

        [ForeignKey("OrderId")]
        public virtual Order Order { get; set; }

        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; }
    }
}
