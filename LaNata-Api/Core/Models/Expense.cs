using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LaNata_Api.Audit.Models;

namespace LaNata_Api.Core.Models
{
    [Table("Expense")]
    public class Expense : AuditableEntity
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("date")]
        public DateTime Date { get; set; }

        [Column("description")]
        public string Description { get; set; }

        [Column("price")]
        public double Price { get; set; }
    }
}
