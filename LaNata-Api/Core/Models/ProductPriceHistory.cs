using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LaNata_Api.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace LaNata_Api.Core.Models
{
    [Table("ProductPriceHistory")]
    public class ProductPriceHistory
    {
        [Key]
        public int Id { get; set; }
        public int ProductId { get; set; }

        [Precision(18, 2)]
        public decimal PreviousPrice { get; set; }

        [Precision(18, 2)]
        public decimal NewPrice { get; set; }

        [Precision(18, 2)]
        public decimal? PreviousBreakPrice { get; set; }

        [Precision(18, 2)]
        public decimal? NewBreakPrice { get; set; }

        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
        public int? ChangedBy { get; set; }

        public int RepricedOrders { get; set; }
        public int RepricedLines { get; set; }
        public int SkippedLines { get; set; }

        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; }

        [ForeignKey("ChangedBy")]
        public virtual User ChangedByUser { get; set; }
    }
}
