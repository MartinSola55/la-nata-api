using System.Collections.Generic;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using LaNata_Api.Report.Helpers.Components;
using LaNata_Api.Report.Models.Constants;
using LaNata_Api.Report.Models.Internal;

namespace LaNata_Api.Report.Helpers.Documents
{
    public class TableReportDocument<T>(ReportTableRequest<T> request, List<T> rows) : ReportDocumentBase(request.Options)
    {
        private readonly ReportTableRequest<T> _request = request;
        private readonly List<T> _rows = rows;

        protected override void ComposeContent(IContainer container)
        {
            container.Column(column =>
            {
                ComposeIntro(column);

                column.Item().Component(new ReportTableComponent<T>(_request.Columns, _rows, _request.StripedRows));

                if (!_options.ShowRowCount)
                    return;

                column.Item()
                    .PaddingTop(ReportDimensions.SpacingMd)
                    .AlignRight()
                    .Text(ReportMessages.TotalRecords() + ": " + _rows.Count)
                    .FontSize(ReportDimensions.FontXs)
                    .FontColor(ReportColors.ToColor(ReportColors.TextMuted));
            });
        }
    }
}
