namespace Template_Api.Audit.Models.Constants
{
    // Seeded in audit.AuditCategory: adding a code here also needs a seed row in a SQL migration
    public static class AuditCategoryCode
    {
        public const string SECURITY = "SECURITY";
        public const string DATA = "DATA";
        public const string PAYMENT = "PAYMENT";
        public const string NOTIFICATION = "NOTIFICATION";
        public const string DOCUMENT = "DOCUMENT";
    }
}
