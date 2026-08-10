using QuestPDF.Infrastructure;

namespace Template_Api.Report.Models.Constants
{
    public static class ReportColors
    {
        public const string Primary = "#245A73";
        public const string PrimarySoft = "#E9EEF5";
        public const string TextPrimary = "#1B1B1D";
        public const string TextSecondary = "#4A4A52";
        public const string TextMuted = "#70707A";
        public const string TextOnPrimary = "#FFFFFF";
        public const string Border = "#D5D9E0";
        public const string BorderSubtle = "#EBEDF1";
        public const string RowStripe = "#F7F8FA";
        public const string Success = "#0B7A3B";
        public const string Warning = "#B26A00";
        public const string Danger = "#C0261B";

        public static Color ToColor(string hex)
        {
            return Color.FromHex(hex);
        }
    }
}
