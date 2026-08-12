using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using System.Collections.Generic;
using LaNata_Api.Core.Models.Internal;
using LaNata_Api.Report.Helpers;
using LaNata_Api.Report.Helpers.Components;
using LaNata_Api.Report.Models.Constants;
using LaNata_Api.Report.Models.Internal;

namespace LaNata_Api.Core.Helpers.Documents
{
    // The balance is the one report that GenerateTable cannot express: it stacks two tables
    // (expenses and income) in a single document. The profit lands in the summary cards.
    public class BalanceReportDocument(BalanceReportData data, ReportOptions options) : ReportDocumentBase(options)
    {
        private readonly BalanceReportData _data = data;

        protected override void ComposeContent(IContainer container)
        {
            container.Column(column =>
            {
                ComposeIntro(column);

                ComposeSection(column, "GASTOS", BuildExpenseColumns(), _data.Expenses, _data.TotalExpenses, isFirst: true);
                ComposeSection(column, "INGRESOS", BuildIncomeColumns(), _data.Income, _data.TotalIncome, isFirst: false);
            });
        }

        #region Private
        private static void ComposeSection<T>(ColumnDescriptor column, string title, List<ReportColumn<T>> columns, List<T> rows, decimal total, bool isFirst)
        {
            column.Item()
                .PaddingTop(isFirst ? 0 : ReportDimensions.SpacingXl)
                .PaddingBottom(ReportDimensions.SpacingSm)
                .Text(title)
                .FontSize(ReportDimensions.FontLg)
                .Bold()
                .FontColor(ReportColors.ToColor(ReportColors.Primary));

            column.Item().Component(new ReportTableComponent<T>(columns, rows));

            column.Item()
                .PaddingTop(ReportDimensions.SpacingSm)
                .AlignRight()
                .Text("Total: " + Currency(total))
                .FontSize(ReportDimensions.FontMd)
                .Bold();
        }

        private static List<ReportColumn<BalanceExpenseRow>> BuildExpenseColumns()
        {
            return ReportColumns
                .For<BalanceExpenseRow>()
                .Add(x => x.Description, "Descripción", width: 3)
                .AddDate(x => x.Date, "Fecha")
                .AddCurrency(x => x.Price, "Monto")
                .Build();
        }

        private static List<ReportColumn<BalanceIncomeRow>> BuildIncomeColumns()
        {
            return ReportColumns
                .For<BalanceIncomeRow>()
                .Add(x => x.ClientName, "Cliente", width: 2)
                .Add(x => x.EventAddress, "Dirección", width: 2)
                .AddDate(x => x.Date, "Fecha")
                .Add(x => x.ShipmentPrice.HasValue ? Currency(x.ShipmentPrice.Value) : "-", "Flete", ReportAlignment.Right)
                .AddCurrency(x => x.Total, "Monto")
                .Build();
        }

        private static string Currency(decimal value)
        {
            return value.ToString("C2", ReportCulture.Current);
        }
        #endregion
    }
}
