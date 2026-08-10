using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Template_Api.Export.Models.Constants;
using Template_Api.Export.Models.Internal;

namespace Template_Api.Export.Helpers
{
    internal static class CsvWriter
    {
        private const string LINE_ENDING = "\r\n";
        private const char QUOTE = '"';

        private static readonly char[] FORMULA_TRIGGERS = ['=', '+', '-', '@', '\t', '\r'];

        public static byte[] Write<T>(List<ExportColumn<T>> columns, IEnumerable<T> rows, string delimiter, CultureInfo culture)
        {
            delimiter = string.IsNullOrEmpty(delimiter) ? ExportDelimiter.SEMICOLON : delimiter;

            var builder = new StringBuilder();

            AppendRow(builder, columns.ConvertAll(x => x.Header), delimiter);

            foreach (var row in rows)
            {
                var values = new List<string>(columns.Count);

                foreach (var column in columns)
                    values.Add(ExportValueFormatter.ToText(column.Value(row), column.Format, culture));

                AppendRow(builder, values, delimiter);
            }

            // GetBytes never emits the preamble, so the BOM has to be prepended by hand. Without it
            // Excel falls back to the system codepage and mangles every accent
            var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
            var preamble = encoding.GetPreamble();
            var content = encoding.GetBytes(builder.ToString());
            var bytes = new byte[preamble.Length + content.Length];

            preamble.CopyTo(bytes, 0);
            content.CopyTo(bytes, preamble.Length);

            return bytes;
        }

        #region Private
        private static void AppendRow(StringBuilder builder, List<string> values, string delimiter)
        {
            for (var i = 0; i < values.Count; i++)
            {
                if (i > 0)
                    builder.Append(delimiter);

                builder.Append(Escape(values[i], delimiter));
            }

            builder.Append(LINE_ENDING);
        }

        private static string Escape(string value, string delimiter)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            value = Neutralize(value);

            var needsQuotes = value.Contains(delimiter)
                || value.Contains(QUOTE)
                || value.Contains('\n')
                || value.Contains('\r')
                || value[0] == ' '
                || value[^1] == ' ';

            if (!needsQuotes)
                return value;

            return QUOTE + value.Replace("\"", "\"\"") + QUOTE;
        }

        // CSV injection guard: a cell starting with = + - @ runs as a formula when the file is
        // opened, which turns exported user input into code
        private static string Neutralize(string value)
        {
            return System.Array.IndexOf(FORMULA_TRIGGERS, value[0]) >= 0 ? "'" + value : value;
        }
        #endregion
    }
}
