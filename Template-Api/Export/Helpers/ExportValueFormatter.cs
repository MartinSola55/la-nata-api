using System;
using System.Globalization;
using Template_Api.Export.Models.Internal;

namespace Template_Api.Export.Helpers
{
    internal static class ExportValueFormatter
    {
        private const string DATE_PATTERN = "dd/MM/yyyy";
        private const string DATE_TIME_PATTERN = "dd/MM/yyyy HH:mm";

        // Resolved defensively: containers in globalization-invariant mode reject named cultures
        public static readonly CultureInfo DefaultCulture = ResolveDefaultCulture();

        public static string ToText(object value, ExportValueFormat format, CultureInfo culture)
        {
            if (value == null)
                return string.Empty;

            culture ??= DefaultCulture;

            return format switch
            {
                ExportValueFormat.Text => Convert.ToString(value, culture),
                ExportValueFormat.Integer => Convert.ToInt64(value, culture).ToString("N0", culture),
                ExportValueFormat.Decimal => Convert.ToDecimal(value, culture).ToString("N2", culture),
                ExportValueFormat.Currency => Convert.ToDecimal(value, culture).ToString("C2", culture),
                ExportValueFormat.Percentage => Convert.ToDecimal(value, culture).ToString("N2", culture) + "%",
                ExportValueFormat.Date => Convert.ToDateTime(value, culture).ToString(DATE_PATTERN, culture),
                ExportValueFormat.DateTime => Convert.ToDateTime(value, culture).ToString(DATE_TIME_PATTERN, culture),
                ExportValueFormat.YesNo => Convert.ToBoolean(value, culture) ? "Sí" : "No",
                _ => FormatUnspecified(value, culture)
            };
        }

        // Excel stores the raw value and applies this at display time, so the file reads correctly
        // in any locale
        public static string ToExcelNumberFormat(ExportValueFormat format)
        {
            return format switch
            {
                ExportValueFormat.Integer => "#,##0",
                ExportValueFormat.Decimal => "#,##0.00",
                ExportValueFormat.Currency => "\"$\"#,##0.00",
                ExportValueFormat.Percentage => "0.00\"%\"",
                ExportValueFormat.Date => "dd/mm/yyyy",
                ExportValueFormat.DateTime => "dd/mm/yyyy hh:mm",
                _ => null
            };
        }

        public static bool IsTypedFormat(ExportValueFormat format)
        {
            return format is ExportValueFormat.Integer
                or ExportValueFormat.Decimal
                or ExportValueFormat.Currency
                or ExportValueFormat.Percentage
                or ExportValueFormat.Date
                or ExportValueFormat.DateTime;
        }

        #region Private
        private static string FormatUnspecified(object value, CultureInfo culture)
        {
            return value switch
            {
                bool flag => flag ? "Sí" : "No",
                DateTime date => date.TimeOfDay == TimeSpan.Zero
                    ? date.ToString(DATE_PATTERN, culture)
                    : date.ToString(DATE_TIME_PATTERN, culture),
                decimal number => number.ToString("N2", culture),
                double number => number.ToString("N2", culture),
                float number => number.ToString("N2", culture),
                _ => Convert.ToString(value, culture)
            };
        }

        private static CultureInfo ResolveDefaultCulture()
        {
            try
            {
                return CultureInfo.GetCultureInfo("es-AR");
            }
            catch (CultureNotFoundException)
            {
                return CultureInfo.InvariantCulture;
            }
        }
        #endregion
    }
}
