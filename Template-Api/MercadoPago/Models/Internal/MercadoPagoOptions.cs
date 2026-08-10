namespace Template_Api.MercadoPago.Models.Internal
{
    public class MercadoPagoOptions
    {
        public bool Enabled { get; set; }
        public string BaseUrl { get; set; } = "https://api.mercadopago.com";
        public string AccessToken { get; set; }
        public string PublicKey { get; set; }
        public string WebhookSecret { get; set; }
        public string CurrencyId { get; set; } = "ARS";
        public string SuccessUrl { get; set; }
        public string PendingUrl { get; set; }
        public string FailureUrl { get; set; }
        public string NotificationUrl { get; set; }
        public string AutoReturn { get; set; } = "approved";
        public string StatementDescriptor { get; set; }
        public int WebhookToleranceSeconds { get; set; } = 300;
    }
}
