using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LaNata_Api.Audit.Models;

namespace LaNata_Api.Core.Models
{
    [Table("Order")]
    public class Order : AuditableEntity
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("client_name")]
        public string ClientName { get; set; }

        [Column("phone")]
        public string Phone { get; set; }

        // The legacy column name carries a typo; the property does not, so the mapping is explicit
        [Column("event_adress")]
        public string EventAddress { get; set; }

        // Every existing row stores the event day at 12:00. Filter by day range, never by equality.
        [Column("date")]
        public DateTime Date { get; set; }

        [Column("shipment_price")]
        public double? ShipmentPrice { get; set; }

        [Column("observation")]
        public string Observation { get; set; }

        public virtual List<ProductOrder> Products { get; set; } = [];
    }
}
