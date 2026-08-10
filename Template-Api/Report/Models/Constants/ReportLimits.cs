namespace Template_Api.Report.Models.Constants
{
    public static class ReportLimits
    {
        // Lower than the export cap: PDF layout is far more expensive than a spreadsheet row
        public const int MaxRows = 20000;
    }
}
