using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Template_Api.MercadoPago.Models.DTO
{
    public class MercadoPagoWebhookRequest
    {
        [JsonPropertyName("action")]
        public string Action { get; set; }

        [JsonPropertyName("api_version")]
        public string ApiVersion { get; set; }

        [JsonPropertyName("data")]
        public MercadoPagoWebhookData Data { get; set; }

        [JsonPropertyName("date_created")]
        public DateTime? DateCreated { get; set; }

        [JsonPropertyName("id")]
        public JsonElement Id { get; set; }

        [JsonPropertyName("live_mode")]
        public bool LiveMode { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("user_id")]
        public long? UserId { get; set; }
    }

    public class MercadoPagoWebhookData
    {
        [JsonPropertyName("id")]
        public JsonElement Id { get; set; }
    }

    public class ProcessWebhookResponse
    {
        public int Id { get; set; }
        public string PaymentId { get; set; }
        public string ExternalReference { get; set; }
        public string Status { get; set; }
        public string StatusDetail { get; set; }
        public DateTime ProcessedAt { get; set; }
    }
}
