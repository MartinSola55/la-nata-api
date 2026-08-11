namespace LaNata_Api.Audit.Models.Constants
{
    // Seeded in audit.AuditCategory: adding a code here also needs a seed row in a SQL migration.
    // These are the values AuditGetAllRequest.CategoryCodes filters by.
    public static class AuditCategoryCode
    {
        public const string SECURITY = "SECURITY";
        public const string DATA = "DATA";
        public const string DOCUMENT = "DOCUMENT";
    }
}
