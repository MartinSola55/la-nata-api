using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using LaNata_Api.Report.Models.Constants;
using LaNata_Api.Report.Models.Internal;

namespace LaNata_Api.Report.Helpers.Components
{
    public class ReportHeaderComponent(ReportOptions options) : IComponent
    {
        private readonly ReportOptions _options = options;

        public void Compose(IContainer container)
        {
            container
                .PaddingBottom(ReportDimensions.SpacingLg)
                .Row(row =>
                {
                    row.RelativeItem().Column(left =>
                    {
                        if (_options.Logo != null && _options.Logo.Length > 0)
                        {
                            left.Item()
                                .MaxWidth(230)
                                .MaxHeight(ReportDimensions.LogoMaxHeight)
                                .Image(_options.Logo)
                                .FitArea();
                        }
                        else
                        {
                            left.Item()
                                .Text(_options.Title ?? string.Empty)
                                .FontSize(ReportDimensions.FontXl)
                                .Bold()
                                .FontColor(ReportColors.ToColor(ReportColors.Primary));
                        }

                        if (!string.IsNullOrWhiteSpace(_options.Subtitle))
                        {
                            left.Item()
                                .PaddingTop(ReportDimensions.SpacingXs)
                                .Text(_options.Subtitle)
                                .FontSize(ReportDimensions.FontMd)
                                .Italic()
                                .SemiBold()
                                .FontColor(ReportColors.ToColor(ReportColors.TextPrimary));
                        }
                    });

                    row.RelativeItem().AlignRight().Column(right =>
                    {
                        foreach (var line in _options.ContactLines)
                        {
                            right.Item()
                                .AlignRight()
                                .Text(line)
                                .FontSize(ReportDimensions.FontSm)
                                .SemiBold()
                                .FontColor(ReportColors.ToColor(ReportColors.TextSecondary));
                        }
                    });
                });
        }
    }
}
