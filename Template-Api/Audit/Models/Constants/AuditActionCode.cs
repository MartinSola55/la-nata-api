namespace Template_Api.Audit.Models.Constants
{
    // Seeded in audit.AuditAction: adding a code here also needs a seed row in a SQL migration
    public static class AuditActionCode
    {
        public const string CREATED = "CREATED";
        public const string UPDATED = "UPDATED";
        public const string DELETED = "DELETED";
        public const string ACTIVATED = "ACTIVATED";
        public const string DEACTIVATED = "DEACTIVATED";
        public const string LOGIN = "LOGIN";
        public const string LOGIN_FAILED = "LOGIN_FAILED";
        public const string PASSWORD_CHANGED = "PASSWORD_CHANGED";
        public const string PAYMENT_CREATED = "PAYMENT_CREATED";
        public const string PAYMENT_UPDATED = "PAYMENT_UPDATED";
        public const string EMAIL_SENT = "EMAIL_SENT";
        public const string WHATSAPP_SENT = "WHATSAPP_SENT";
        public const string EXPORTED = "EXPORTED";
        public const string REPORT_GENERATED = "REPORT_GENERATED";
    }
}
