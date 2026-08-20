namespace LaNata_Api.Audit.Models.Constants
{
    // Entity is a free string column, so a new entity only needs a constant here, not a migration
    public static class AuditEntityCode
    {
        public const string USER = "USER";
        public const string CATEGORY = "CATEGORY";
        public const string PRODUCT = "PRODUCT";
        public const string ORDER = "ORDER";
        public const string EXPENSE = "EXPENSE";
    }
}
