using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Template_Api.Audit.Models;

namespace Template_Api.Core.Models
{
    public class Cart : AuditableEntity
    {
        [Key]
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Status { get; set; }
        public decimal Total { get; set; }
        public DateTime? PaidAt { get; set; }

        [ForeignKey("UserId")]
        public virtual User User { get; set; }
        public virtual List<CartProduct> Products { get; set; } = [];
    }
}
