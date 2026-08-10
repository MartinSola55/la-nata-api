using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Template_Api.Report.Helpers.Components;
using Template_Api.Report.Models.Constants;
using Template_Api.Report.Models.Internal;

namespace Template_Api.Report.Helpers
{
    public abstract class ReportDocumentBase(ReportOptions options) : IDocument
    {
        protected readonly ReportOptions _options = options ?? new ReportOptions();

        public void Compose(IDocumentContainer container)
        {
            container.Page(page =>
            {
                page.Size(ResolvePageSize());
                page.Margin(_options.Margin);
                page.DefaultTextStyle(style => style
                    .FontSize(ReportDimensions.FontSm)
                    .FontColor(ReportColors.ToColor(ReportColors.TextPrimary)));

                page.Header().Component(new ReportHeaderComponent(_options));
                page.Content().PaddingVertical(ReportDimensions.SpacingLg).Element(ComposeContent);
                page.Footer().Component(new ReportFooterComponent(_options));
            });
        }

        public DocumentMetadata GetMetadata()
        {
            return new DocumentMetadata
            {
                Title = _options.Title,
                Author = _options.Author,
                Subject = _options.Subtitle,
                CreationDate = _options.GeneratedAt
            };
        }

        public DocumentSettings GetSettings()
        {
            return DocumentSettings.Default;
        }

        protected abstract void ComposeContent(IContainer container);

        protected void ComposeIntro(ColumnDescriptor column)
        {
            if (_options.Filters is { Count: > 0 })
            {
                column.Item()
                    .PaddingBottom(ReportDimensions.SpacingMd)
                    .Component(new ReportFieldsComponent(_options.Filters));
            }

            if (_options.Summary is { Count: > 0 })
            {
                column.Item()
                    .PaddingBottom(ReportDimensions.SpacingMd)
                    .Component(new ReportFieldsComponent(_options.Summary, highlight: true));
            }
        }

        private PageSize ResolvePageSize()
        {
            var size = _options.PageSize switch
            {
                ReportPageSize.Letter => PageSizes.Letter,
                ReportPageSize.Legal => PageSizes.Legal,
                _ => PageSizes.A4
            };

            return _options.Orientation == ReportOrientation.Landscape ? size.Landscape() : size.Portrait();
        }
    }
}
