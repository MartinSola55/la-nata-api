namespace Template_Api.Export.Models.Constants
{
    public static class ExportContentType
    {
        public const string CSV = "text/csv";
        public const string EXCEL = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    }

    public static class ExportFileExtension
    {
        public const string CSV = ".csv";
        public const string EXCEL = ".xlsx";
    }

    public static class ExportDelimiter
    {
        public const string SEMICOLON = ";";
        public const string COMMA = ",";
        public const string TAB = "\t";
        public const string PIPE = "|";
    }
}
