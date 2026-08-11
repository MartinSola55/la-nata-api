using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using LaNata_Api.Report.Models.Constants;
using LaNata_Api.Report.Models.Internal;

namespace LaNata_Api.Report.Helpers.Components
{
    public class ReportFooterComponent(ReportOptions options) : IComponent
    {
        private readonly ReportOptions _options = options;

        public void Compose(IContainer container)
        {
            container
                .BorderTop(ReportDimensions.BorderThin)
                .BorderColor(ReportColors.ToColor(ReportColors.Border))
                .PaddingTop(ReportDimensions.SpacingSm)
                .Row(row =>
                {
                    row.RelativeItem()
                        .Text(BuildStamp())
                        .FontSize(ReportDimensions.FontXs)
                        .FontColor(ReportColors.ToColor(ReportColors.TextMuted));

                    if (!_options.ShowPageNumbers)
                        return;

                    row.ConstantItem(120).AlignRight().Text(text =>
                    {
                        text.DefaultTextStyle(style => style
                            .FontSize(ReportDimensions.FontXs)
                            .FontColor(ReportColors.ToColor(ReportColors.TextMuted)));

                        text.Span(ReportMessages.Page() + " ");
                        text.CurrentPageNumber();
                        text.Span(" " + ReportMessages.Of() + " ");
                        text.TotalPages();
                    });
                });
        }

        private string BuildStamp()
        {
            var stamp = ReportMessages.GeneratedAt() + " " + _options.GeneratedAt.ToString("dd/MM/yyyy HH:mm", ReportCulture.Current) + " hs";

            if (!string.IsNullOrWhiteSpace(_options.Author))
                stamp += " · " + _options.Author;

            if (!string.IsNullOrWhiteSpace(_options.FooterNote))
                stamp += " · " + _options.FooterNote;

            return stamp;
        }
    }
}
