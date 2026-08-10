using System;
using System.ComponentModel.DataAnnotations;

namespace Template_Api.WhatsApp.Models
{
    public class WhatsAppMessage
    {
        [Key]
        public int Id { get; set; }
        public string PhoneNumber { get; set; }
        public string Message { get; set; }
        public string Provider { get; set; }
        public string Status { get; set; }
        public string ProviderResponse { get; set; }
        public string Error { get; set; }
        public DateTime? SentAt { get; set; }
    }
}
