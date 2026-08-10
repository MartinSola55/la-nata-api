using System;

namespace Template_Api.MercadoPago.Models.DTO
{
    public class GetPaymentRequest
    {
        public int? Id { get; set; }
        public string ExternalReference { get; set; }
        public string PaymentId { get; set; }
    }

    public class GetPaymentResponse
    {
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
        public DateTime? PaidAt { get; set; }
        public DateTime? LastNotificationAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
