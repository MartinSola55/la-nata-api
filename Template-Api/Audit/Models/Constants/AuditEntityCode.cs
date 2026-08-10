namespace Template_Api.Audit.Models.Constants
{
    // Entity is a free string column, so a new entity only needs a constant here, not a migration
    public static class AuditEntityCode
    {
        public const string USER = "USER";
        public const string GENERIC_ITEM = "GENERIC_ITEM";
        public const string PRODUCT = "PRODUCT";
        public const string CART = "CART";
        public const string MERCADO_PAGO_PAYMENT = "MERCADO_PAGO_PAYMENT";
    }
}
