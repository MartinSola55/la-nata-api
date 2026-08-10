using SpreadCheetah;
using SpreadCheetah.Styling;
using SpreadCheetah.Worksheets;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Template_Api.Export.Models.Constants;
using Template_Api.Export.Models.Internal;

namespace Template_Api.Export.Helpers
{
    internal static class ExcelWriter
    {
        private const double DEFAULT_MIN_WIDTH = 12;
        private const double DEFAULT_MAX_WIDTH = 60;

        public static async Task<byte[]> Write<T>(List<ExportColumn<T>> columns, IEnumerable<T> rows, string sheetName, bool freezeHeader, CultureInfo culture)
        {
            using var stream = new MemoryStream();

            await using (var spreadsheet = await Spreadsheet.CreateNewAsync(stream))
            {
                var headerStyleId = spreadsheet.AddStyle(BuildHeaderStyle());
                var columnStyleIds = new List<StyleId>(columns.Count);

                foreach (var column in columns)
                    columnStyleIds.Add(BuildColumnStyleId(spreadsheet, column.Format));

                await spreadsheet.StartWorksheetAsync(SanitizeSheetName(sheetName), BuildWorksheetOptions(columns, freezeHeader));
                await spreadsheet.AddHeaderRowAsync(columns.ConvertAll(x => x.Header), headerStyleId);

                foreach (var row in rows)
                {
                    var cells = new Cell[columns.Count];

                    for (var i = 0; i < columns.Count; i++)
                        cells[i] = BuildCell(columns[i].Value(row), columns[i].Format, columnStyleIds[i], culture);

                    await spreadsheet.AddRowAsync(cells);
                }

                await spreadsheet.FinishAsync();
            }

            return stream.ToArray();
        }

        public static async Task<byte[]> Write(List<ExportSheet> sheets, CultureInfo culture)
        {
            using var stream = new MemoryStream();

            await using (var spreadsheet = await Spreadsheet.CreateNewAsync(stream))
            {
                var headerStyleId = spreadsheet.AddStyle(BuildHeaderStyle());

                foreach (var sheet in sheets)
                {
                    var styleIds = new List<StyleId>(sheet.Headers.Count);

                    for (var i = 0; i < sheet.Headers.Count; i++)
                        styleIds.Add(BuildColumnStyleId(spreadsheet, GetFormat(sheet, i)));

                    await spreadsheet.StartWorksheetAsync(SanitizeSheetName(sheet.SheetName), BuildWorksheetOptions(sheet));
                    await spreadsheet.AddHeaderRowAsync(sheet.Headers, headerStyleId);

                    foreach (var row in sheet.Rows)
                    {
                        var cells = new Cell[sheet.Headers.Count];

                        for (var i = 0; i < sheet.Headers.Count; i++)
                        {
                            var value = i < row.Count ? row[i] : null;
                            cells[i] = BuildCell(value, GetFormat(sheet, i), styleIds[i], culture);
                        }

                        await spreadsheet.AddRowAsync(cells);
                    }
                }

                await spreadsheet.FinishAsync();
            }

            return stream.ToArray();
        }

        #region Private
        private static Cell BuildCell(object value, ExportValueFormat format, StyleId styleId, CultureInfo culture)
        {
            if (value == null)
                return new Cell(string.Empty, styleId);

            // Text format is an explicit request to keep the value as-is, so codes and identifiers
            // do not lose their leading zeros
            if (format == ExportValueFormat.Text)
                return new Cell(Convert.ToString(value, culture), styleId);

            if (ExportValueFormatter.IsTypedFormat(format))
                return BuildTypedCell(value, format, styleId, culture);

            return value switch
            {
                bool flag => new Cell(flag, styleId),
                DateTime date => new Cell(date, styleId),
                int number => new Cell(number, styleId),
                long number => new Cell(number, styleId),
                decimal number => new Cell(number, styleId),
                double number => new Cell(number, styleId),
                float number => new Cell(number, styleId),
                _ => new Cell(ExportValueFormatter.ToText(value, format, culture), styleId)
            };
        }

        private static Cell BuildTypedCell(object value, ExportValueFormat format, StyleId styleId, CultureInfo culture)
        {
            try
            {
                return format switch
                {
                    ExportValueFormat.Integer => new Cell(Convert.ToInt64(value, culture), styleId),
                    ExportValueFormat.Date or ExportValueFormat.DateTime => new Cell(Convert.ToDateTime(value, culture), styleId),
                    _ => new Cell(Convert.ToDecimal(value, culture), styleId)
                };
            }
            catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
            {
                // A value that does not fit the declared format must not abort the whole export
                return new Cell(ExportValueFormatter.ToText(value, ExportValueFormat.None, culture), styleId);
            }
        }

        private static Style BuildHeaderStyle()
        {
            return new Style
            {
                Font = { Bold = true },
                Fill = { Color = System.Drawing.Color.FromArgb(0xE9, 0xED, 0xF2) }
            };
        }

        private static StyleId BuildColumnStyleId(Spreadsheet spreadsheet, ExportValueFormat format)
        {
            var numberFormat = ExportValueFormatter.ToExcelNumberFormat(format);
            var style = new Style();

            if (numberFormat != null)
                style.Format = NumberFormat.Custom(numberFormat);

            return spreadsheet.AddStyle(style);
        }

        private static WorksheetOptions BuildWorksheetOptions<T>(List<ExportColumn<T>> columns, bool freezeHeader)
        {
            var options = new WorksheetOptions();

            if (freezeHeader)
                options.FrozenRows = 1;

            for (var i = 0; i < columns.Count; i++)
                options.Column(i + 1).Width = columns[i].Width ?? EstimateWidth(columns[i].Header);

            options.AutoFilter = new AutoFilterOptions(GetHeaderRange(columns.Count));

            return options;
        }

        private static WorksheetOptions BuildWorksheetOptions(ExportSheet sheet)
        {
            var options = new WorksheetOptions();

            if (sheet.FreezeHeader)
                options.FrozenRows = 1;

            for (var i = 0; i < sheet.Headers.Count; i++)
                options.Column(i + 1).Width = EstimateWidth(sheet.Headers[i]);

            options.AutoFilter = new AutoFilterOptions(GetHeaderRange(sheet.Headers.Count));

            return options;
        }

        private static string GetHeaderRange(int columnCount)
        {
            return "A1:" + GetColumnName(columnCount) + "1";
        }

        private static string GetColumnName(int columnNumber)
        {
            var name = string.Empty;

            while (columnNumber > 0)
            {
                var remainder = (columnNumber - 1) % 26;
                name = (char)('A' + remainder) + name;
                columnNumber = (columnNumber - 1) / 26;
            }

            return name;
        }

        private static double EstimateWidth(string header)
        {
            var width = (header?.Length ?? 0) + 4;

            return Math.Clamp(width, DEFAULT_MIN_WIDTH, DEFAULT_MAX_WIDTH);
        }

        private static ExportValueFormat GetFormat(ExportSheet sheet, int index)
        {
            return index < sheet.Formats.Count ? sheet.Formats[index] : ExportValueFormat.None;
        }

        private static string SanitizeSheetName(string sheetName)
        {
            if (string.IsNullOrWhiteSpace(sheetName))
                return "Hoja1";

            foreach (var invalid in new[] { ':', '\\', '/', '?', '*', '[', ']' })
                sheetName = sheetName.Replace(invalid, ' ');

            sheetName = sheetName.Trim();

            return sheetName.Length > ExportLimits.MaxSheetNameLength
                ? sheetName[..ExportLimits.MaxSheetNameLength]
                : sheetName;
        }
        #endregion
    }
}
