using System;
using System.Collections.Generic;
using LaNata_Api.Report.Models.Constants;

namespace LaNata_Api.Report.Models.Internal
{
    public enum ReportOrientation
    {
        Portrait = 0,
        Landscape
    }

    public enum ReportPageSize
    {
        A4 = 0,
        Letter,
        Legal
    }

    public class ReportOptions
    {
        public string Title { get; set; }
        public string Subtitle { get; set; }

        public string Author { get; set; }

        public string FooterNote { get; set; }

        public byte[] Logo { get; set; }

        public ReportOrientation Orientation { get; set; } = ReportOrientation.Portrait;
        public ReportPageSize PageSize { get; set; } = ReportPageSize.A4;
        public float Margin { get; set; } = ReportDimensions.PageMargin;

        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

        public List<ReportField> Filters { get; set; } = [];

        public List<ReportField> Summary { get; set; } = [];

        public bool ShowPageNumbers { get; set; } = true;
        public bool ShowRowCount { get; set; } = true;
    }

    public class ReportField
    {
        public string Label { get; set; }
        public string Value { get; set; }

        public ReportField()
        {
        }

        public ReportField(string label, string value)
        {
            Label = label;
            Value = value;
        }
    }
}
