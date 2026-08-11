namespace LaNata_Api.Export.Models.Constants
{
    public static class ExportLimits
    {
        public const int MaxRows = 100000;
        // Excel rejects sheet names longer than this, and a few characters on top
        public const int MaxSheetNameLength = 31;
    }
}
