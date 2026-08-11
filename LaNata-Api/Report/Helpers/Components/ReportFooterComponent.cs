using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using LaNata_Api.Report.Models.Constants;
using LaNata_Api.Report.Models.Internal;

namespace LaNata_Api.Report.Helpers.Components
{
    public class ReportFooterComponent(ReportOptions options) : IComponent
    {
        private const float QrSize = 64f;
        private const float QrGap = 32f;

        private readonly ReportOptions _options = options;

        public void Compose(IContainer container)
        {
            container.Column(column =>
            {
                if (HasQrCodes())
                    column.Item().PaddingBottom(ReportDimensions.SpacingLg).Element(ComposeQrCodes);

                column.Item()
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
            });
        }

        private void ComposeQrCodes(IContainer container)
        {
            container.AlignRight().Row(row =>
            {
                if (_options.WhatsAppQr?.Length > 0)
                    row.ConstantItem(QrSize).Image(_options.WhatsAppQr).FitArea();

                if (_options.WhatsAppQr?.Length > 0 && _options.InstagramQr?.Length > 0)
                    row.ConstantItem(QrGap);

                if (_options.InstagramQr?.Length > 0)
                    row.ConstantItem(QrSize).Image(_options.InstagramQr).FitArea();
            });
        }

        private bool HasQrCodes()
        {
            return _options.WhatsAppQr?.Length > 0 || _options.InstagramQr?.Length > 0;
        }

        private string BuildStamp()
        {
            if (!string.IsNullOrWhiteSpace(_options.FooterNote))
                return _options.FooterNote;

            var stamp = ReportMessages.GeneratedAt() + " " + _options.GeneratedAt.ToString("dd/MM/yyyy HH:mm", ReportCulture.Current) + " hs";

            if (!string.IsNullOrWhiteSpace(_options.Author))
                stamp += " · " + _options.Author;

            return stamp;
        }
    }
}
