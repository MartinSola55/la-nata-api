using System.Collections.Generic;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Template_Api.Report.Models.Constants;
using Template_Api.Report.Models.Internal;

namespace Template_Api.Report.Helpers.Components
{
    public class ReportTableComponent<T>(List<ReportColumn<T>> columns, List<T> rows, bool striped = true) : IComponent
    {
        private readonly List<ReportColumn<T>> _columns = columns;
        private readonly List<T> _rows = rows;
        private readonly bool _striped = striped;

        public void Compose(IContainer container)
        {
            if (_rows.Count == 0)
            {
                container
                    .Border(ReportDimensions.BorderThin)
                    .BorderColor(ReportColors.ToColor(ReportColors.Border))
                    .Padding(ReportDimensions.SpacingXl)
                    .AlignCenter()
                    .Text(ReportMessages.EmptyTable())
                    .FontSize(ReportDimensions.FontMd)
                    .FontColor(ReportColors.ToColor(ReportColors.TextMuted));

                return;
            }

            container.Table(table =>
            {
                table.ColumnsDefinition(definition =>
                {
                    foreach (var column in _columns)
                    {
                        if (column.Sizing == ReportColumnSizing.Constant)
                            definition.ConstantColumn(column.Width);
                        else
                            definition.RelativeColumn(column.Width);
                    }
                });

                table.Header(header =>
                {
                    foreach (var column in _columns)
                    {
                        header.Cell()
                            .Background(ReportColors.ToColor(ReportColors.Primary))
                            .Padding(ReportDimensions.CellPaddingY)
                            .AlignmentFor(column.Alignment)
                            .Text(column.Header ?? string.Empty)
                            .FontSize(ReportDimensions.FontSm)
                            .Bold()
                            .FontColor(ReportColors.ToColor(ReportColors.TextOnPrimary));
                    }
                });

                for (var rowIndex = 0; rowIndex < _rows.Count; rowIndex++)
                {
                    var row = _rows[rowIndex];
                    var isStriped = _striped && rowIndex % 2 == 1;

                    foreach (var column in _columns)
                    {
                        var cell = table.Cell()
                            .BorderBottom(ReportDimensions.BorderThin)
                            .BorderColor(ReportColors.ToColor(ReportColors.BorderSubtle));

                        if (isStriped)
                            cell = cell.Background(ReportColors.ToColor(ReportColors.RowStripe));

                        cell.PaddingVertical(ReportDimensions.CellPaddingY)
                            .PaddingHorizontal(ReportDimensions.CellPaddingX)
                            .AlignmentFor(column.Alignment)
                            .Text(column.Value(row) ?? string.Empty)
                            .FontSize(ReportDimensions.FontSm)
                            .FontColor(ReportColors.ToColor(ReportColors.TextPrimary));
                    }
                }
            });
        }
    }

    internal static class ReportTableExtensions
    {
        public static IContainer AlignmentFor(this IContainer container, ReportAlignment alignment)
        {
            return alignment switch
            {
                ReportAlignment.Center => container.AlignCenter(),
                ReportAlignment.Right => container.AlignRight(),
                _ => container.AlignLeft()
            };
        }
    }
}
