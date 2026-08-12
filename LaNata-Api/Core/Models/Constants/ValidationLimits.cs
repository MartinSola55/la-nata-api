namespace LaNata_Api.Core.Models.Constants
{
    public static class ValidationLimits
    {
        public const int ProductDescriptionMaxLength = 255;
        public const int ProductStockMax = 1000000;
        public const decimal ProductPriceMax = 1000000000;

        public const int OrderClientNameMaxLength = 150;
        public const int OrderAddressMaxLength = 255;
        public const int OrderObservationMaxLength = 1000;
        public const int OrderPhoneMaxLength = 20;
        public const int OrderQuantityMax = 100000;
        public const decimal OrderShipmentPriceMax = 100000000;

        public const int ExpenseDescriptionMaxLength = 500;
        public const decimal ExpensePriceMax = 100000000;

        public const int UserEmailMaxLength = 60;
        public const int PasswordMinLength = 8;
        public const int PasswordMaxLength = 128;
        public const int RoleNameMaxLength = 100;

        public const int SearchMaxLength = 100;
        public const int SortColumnMaxLength = 50;
    }
}
