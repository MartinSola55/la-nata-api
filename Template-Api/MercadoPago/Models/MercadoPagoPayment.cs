using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Template_Api.Audit.Models;
using Template_Api.Core.Models;

namespace Template_Api.MercadoPago.Models
{
    public class MercadoPagoPayment : AuditableEntity
    {
        [Key]
        public int Id { get; set; }
        public int? UserId { get; set; }
        public int? CartId { get; set; }
        public string PreferenceId { get; set; }
        public string PaymentId { get; set; }
        public string ExternalReference { get; set; }
        public string PayerEmail { get; set; }
        public decimal TransactionAmount { get; set; }
        public string CurrencyId { get; set; }
        public string Status { get; set; }
        public string StatusDetail { get; set; }
        public string InitPoint { get; set; }
        public string SandboxInitPoint { get; set; }
        public string ProviderResponse { get; set; }
        public string Error { get; set; }
        public DateTime? PaidAt { get; set; }
        public DateTime? LastNotificationAt { get; set; }

        [ForeignKey("CartId")]
        public virtual Cart Cart { get; set; }
    }
}
