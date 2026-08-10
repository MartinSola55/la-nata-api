using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Template_Api.Report.Models.Constants;
using Template_Api.Report.Models.Internal;

namespace Template_Api.Report.Helpers.Components
{
    public class ReportHeaderComponent(ReportOptions options) : IComponent
    {
        private readonly ReportOptions _options = options;

        public void Compose(IContainer container)
        {
            container
                .BorderBottom(ReportDimensions.BorderRegular)
                .BorderColor(ReportColors.ToColor(ReportColors.Primary))
                .PaddingBottom(ReportDimensions.SpacingMd)
                .Row(row =>
                {
                    row.RelativeItem().Column(column =>
                    {
                        column.Item()
                            .Text(_options.Title ?? string.Empty)
                            .FontSize(ReportDimensions.FontXl)
                            .Bold()
                            .FontColor(ReportColors.ToColor(ReportColors.Primary));

                        if (!string.IsNullOrWhiteSpace(_options.Subtitle))
                        {
                            column.Item()
                                .PaddingTop(ReportDimensions.SpacingXs)
                                .Text(_options.Subtitle)
                                .FontSize(ReportDimensions.FontMd)
                                .FontColor(ReportColors.ToColor(ReportColors.TextMuted));
                        }
                    });

                    if (_options.Logo != null && _options.Logo.Length > 0)
                    {
                        row.ConstantItem(120)
                            .AlignRight()
                            .AlignMiddle()
                            .MaxHeight(ReportDimensions.LogoMaxHeight)
                            .Image(_options.Logo)
                            .FitHeight();
                    }
                });
        }
    }
}
