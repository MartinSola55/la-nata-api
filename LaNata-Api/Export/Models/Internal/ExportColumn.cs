using System;

namespace LaNata_Api.Export.Models.Internal
{
    public class ExportColumn<T>
    {
        public string Header { get; set; }
        public Func<T, object> Value { get; set; }
        public ExportValueFormat Format { get; set; }

        // Excel column width in characters. Null lets the writer size it from the header
        public double? Width { get; set; }
    }
}
