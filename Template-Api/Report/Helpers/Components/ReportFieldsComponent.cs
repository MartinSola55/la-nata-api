using System.Collections.Generic;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Template_Api.Report.Models.Constants;
using Template_Api.Report.Models.Internal;

namespace Template_Api.Report.Helpers.Components
{
    public class ReportFieldsComponent(List<ReportField> fields, bool highlight = false) : IComponent
    {
        private const int COLUMNS_PER_ROW = 4;

        private readonly List<ReportField> _fields = fields;
        private readonly bool _highlight = highlight;

        public void Compose(IContainer container)
        {
            if (_fields == null || _fields.Count == 0)
                return;

            container.Column(column =>
            {
                for (var start = 0; start < _fields.Count; start += COLUMNS_PER_ROW)
                {
                    var slice = _fields.GetRange(start, System.Math.Min(COLUMNS_PER_ROW, _fields.Count - start));

                    column.Item().PaddingTop(start == 0 ? 0 : ReportDimensions.SpacingSm).Row(row =>
                    {
                        foreach (var field in slice)
                            row.RelativeItem().PaddingRight(ReportDimensions.SpacingSm).Element(cell => ComposeField(cell, field));

                        // Keeps the last row aligned with the ones above instead of stretching
                        for (var i = slice.Count; i < COLUMNS_PER_ROW; i++)
                            row.RelativeItem();
                    });
                }
            });
        }

        private void ComposeField(IContainer container, ReportField field)
        {
            container
                .Background(ReportColors.ToColor(_highlight ? ReportColors.PrimarySoft : ReportColors.RowStripe))
                .Border(ReportDimensions.BorderThin)
                .BorderColor(ReportColors.ToColor(ReportColors.BorderSubtle))
                .Padding(ReportDimensions.SpacingMd)
                .Column(column =>
                {
                    column.Item()
                        .Text(field.Label ?? string.Empty)
                        .FontSize(ReportDimensions.FontXs)
                        .FontColor(ReportColors.ToColor(ReportColors.TextMuted));

                    column.Item()
                        .PaddingTop(ReportDimensions.SpacingXs)
                        .Text(field.Value ?? "-")
                        .FontSize(_highlight ? ReportDimensions.FontLg : ReportDimensions.FontMd)
                        .SemiBold()
                        .FontColor(ReportColors.ToColor(ReportColors.TextPrimary));
                });
        }
    }
}
