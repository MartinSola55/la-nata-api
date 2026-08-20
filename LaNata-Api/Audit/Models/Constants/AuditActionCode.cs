namespace LaNata_Api.Audit.Models.Constants
{
    // Seeded in audit.AuditAction: adding a code here also needs a seed row in a SQL migration
    public static class AuditActionCode
    {
        public const string CREATED = "CREATED";
        public const string UPDATED = "UPDATED";
        public const string DELETED = "DELETED";
        public const string ORDER_PAID = "ORDER_PAID";
        public const string ORDER_UNPAID = "ORDER_UNPAID";
        public const string LOGIN = "LOGIN";
        public const string LOGIN_FAILED = "LOGIN_FAILED";
        public const string PASSWORD_CHANGED = "PASSWORD_CHANGED";
        public const string EXPORTED = "EXPORTED";
        public const string REPORT_GENERATED = "REPORT_GENERATED";
    }
}
