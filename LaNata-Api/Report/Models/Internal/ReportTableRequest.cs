using System.Collections.Generic;

namespace LaNata_Api.Report.Models.Internal
{
    public class ReportTableRequest<T>
    {
        public string FileName { get; set; }
        public ReportOptions Options { get; set; } = new();
        public List<ReportColumn<T>> Columns { get; set; } = [];
        public IEnumerable<T> Rows { get; set; }

        public bool StripedRows { get; set; } = true;
    }
}
