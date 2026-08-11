using System.Collections.Generic;

namespace LaNata_Api.Export.Models.Internal
{
    // FileName goes without extension: the service appends the one matching the format
    public class ExportRequest<T>
    {
        public string FileName { get; set; }
        public string SheetName { get; set; }
        public List<ExportColumn<T>> Columns { get; set; } = [];
        public IEnumerable<T> Rows { get; set; }

        public bool FreezeHeader { get; set; } = true;

        // Defaults to ";", which is what Excel expects in Spanish locales
        public string Delimiter { get; set; }
    }

    public class ExportSheet
    {
        public string SheetName { get; set; }
        public List<string> Headers { get; set; } = [];
        public List<List<object>> Rows { get; set; } = [];
        public List<ExportValueFormat> Formats { get; set; } = [];
        public bool FreezeHeader { get; set; } = true;
    }

    public class ExportWorkbookRequest
    {
        public string FileName { get; set; }
        public List<ExportSheet> Sheets { get; set; } = [];
    }
}
