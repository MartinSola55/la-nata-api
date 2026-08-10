using System;

namespace Template_Api.Report.Models.Internal
{
    public enum ReportAlignment
    {
        Left = 0,
        Center,
        Right
    }

    // Relative columns share the leftover width in proportion to Width; constant columns take
    // exactly Width points
    public enum ReportColumnSizing
    {
        Relative = 0,
        Constant
    }

    public class ReportColumn<T>
    {
        public string Header { get; set; }
        public Func<T, string> Value { get; set; }
        public ReportAlignment Alignment { get; set; }
        public ReportColumnSizing Sizing { get; set; }
        public float Width { get; set; } = 1f;
    }
}
