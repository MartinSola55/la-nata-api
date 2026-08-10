using System;

namespace Template_Api.WhatsApp.Models.DTO
{
    public class SendTextRequest
    {
        public string PhoneNumber { get; set; }
        public string Message { get; set; }
    }

    public class SendTextResponse
    {
        public int Id { get; set; }
        public string PhoneNumber { get; set; }
        public string Status { get; set; }
        public DateTime? SentAt { get; set; }
    }
}
