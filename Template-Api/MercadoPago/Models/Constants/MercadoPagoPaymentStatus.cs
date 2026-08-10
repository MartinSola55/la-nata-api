namespace Template_Api.MercadoPago.Models.Constants
{
    public static class MercadoPagoPaymentStatus
    {
        public const string PreferenceCreated = "preference_created";
        public const string Pending = "pending";
        public const string Approved = "approved";
        public const string Authorized = "authorized";
        public const string InProcess = "in_process";
        public const string InMediation = "in_mediation";
        public const string Rejected = "rejected";
        public const string Cancelled = "cancelled";
        public const string Refunded = "refunded";
        public const string ChargedBack = "charged_back";
        public const string Unknown = "unknown";
    }
}
