using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LaNata_Api.Audit.Models;
using Microsoft.EntityFrameworkCore;

namespace LaNata_Api.Core.Models
{
    [Table("Expense")]
    public class Expense : AuditableEntity
    {
        [Key]
        public int Id { get; set; }

        public DateTime Date { get; set; }
        public string Description { get; set; }

        [Precision(18, 2)]
        public decimal Price { get; set; }
    }
}
