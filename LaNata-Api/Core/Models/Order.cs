using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LaNata_Api.Audit.Models;
using Microsoft.EntityFrameworkCore;

namespace LaNata_Api.Core.Models
{
    [Table("Order")]
    public class Order : AuditableEntity
    {
        [Key]
        public int Id { get; set; }
        public string ClientName { get; set; }
        public string Phone { get; set; }
        public string EventAddress { get; set; }

        // Every existing row stores the event day at 12:00. Filter by day range, never by equality.
        public DateTime Date { get; set; }

        [Precision(18, 2)]
        public decimal? ShipmentPrice { get; set; }
        public string Observation { get; set; }

        public virtual List<ProductOrder> Products { get; set; } = [];
    }
}
