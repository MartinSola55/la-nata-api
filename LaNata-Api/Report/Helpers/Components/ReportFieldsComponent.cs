using System.Collections.Generic;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using LaNata_Api.Report.Models.Constants;
using LaNata_Api.Report.Models.Internal;

namespace LaNata_Api.Report.Helpers.Components
{
    public class ReportFieldsComponent(List<ReportField> fields, bool highlight = false) : IComponent
    {
        private const int COLUMNS_PER_ROW = 4;

        private const int HIGHLIGHT_COLUMNS_PER_ROW = 2;

        private readonly List<ReportField> _fields = fields;
        private readonly bool _highlight = highlight;

        public void Compose(IContainer container)
        {
            if (_fields == null || _fields.Count == 0)
                return;

            var perRow = _highlight ? HIGHLIGHT_COLUMNS_PER_ROW : COLUMNS_PER_ROW;

            container.Column(column =>
            {
                for (var start = 0; start < _fields.Count; start += perRow)
                {
                    var slice = _fields.GetRange(start, System.Math.Min(perRow, _fields.Count - start));

                    column.Item().PaddingTop(start == 0 ? 0 : ReportDimensions.SpacingXs).Row(row =>
                    {
                        foreach (var field in slice)
                            row.RelativeItem().PaddingRight(ReportDimensions.SpacingSm).Element(cell => ComposeField(cell, field));

                        // Keeps the last row aligned with the ones above instead of stretching
                        for (var i = slice.Count; i < perRow; i++)
                            row.RelativeItem();
                    });
                }
            });
        }

        private void ComposeField(IContainer container, ReportField field)
        {
            container
                .Border(_highlight ? ReportDimensions.BorderRegular : ReportDimensions.BorderThin)
                .BorderColor(ReportColors.ToColor(_highlight ? ReportColors.TextPrimary : ReportColors.Border))
                .PaddingVertical(ReportDimensions.SpacingXs)
                .PaddingHorizontal(ReportDimensions.SpacingSm)
                .Text(text =>
                {
                    text.Span((field.Label ?? string.Empty) + ": ")
                        .FontSize(ReportDimensions.FontXs)
                        .FontColor(ReportColors.ToColor(ReportColors.TextMuted));

                    text.Span(field.Value ?? "-")
                        .FontSize(_highlight ? ReportDimensions.FontLg : ReportDimensions.FontSm)
                        .SemiBold()
                        .FontColor(ReportColors.ToColor(ReportColors.TextPrimary));
                });
        }
    }
}
