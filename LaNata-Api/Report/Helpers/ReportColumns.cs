using System;
using System.Collections.Generic;
using System.Globalization;
using LaNata_Api.Report.Models.Constants;
using LaNata_Api.Report.Models.Internal;

namespace LaNata_Api.Report.Helpers
{
    public class ReportColumns<T>
    {
        private readonly List<ReportColumn<T>> _columns = [];

        internal ReportColumns()
        {
        }

        public ReportColumns<T> Add(Func<T, string> value, string header, ReportAlignment alignment = ReportAlignment.Left, float width = 1f)
        {
            ArgumentNullException.ThrowIfNull(value);

            _columns.Add(new ReportColumn<T>
            {
                Header = header,
                Value = value,
                Alignment = alignment,
                Sizing = ReportColumnSizing.Relative,
                Width = width
            });

            return this;
        }

        public ReportColumns<T> AddFixed(Func<T, string> value, string header, float points, ReportAlignment alignment = ReportAlignment.Left)
        {
            Add(value, header, alignment);
            _columns[^1].Sizing = ReportColumnSizing.Constant;
            _columns[^1].Width = points;

            return this;
        }

        public ReportColumns<T> AddNumber(Func<T, object> value, string header, string format = "N0", float width = 1f)
        {
            return Add(row => Format(value(row), format), header, ReportAlignment.Right, width);
        }

        public ReportColumns<T> AddCurrency(Func<T, object> value, string header, float width = ReportDimensions.ColumnWidthMoney, string emptyText = "")
        {
            return Add(row => value(row) == null ? emptyText : Format(value(row), "C2"), header, ReportAlignment.Right, width);
        }

        public ReportColumns<T> AddDate(Func<T, object> value, string header, bool withTime = false, float width = ReportDimensions.ColumnWidthDate)
        {
            var pattern = withTime ? "dd/MM/yyyy HH:mm" : "dd/MM/yyyy";

            return Add(row => FormatDate(value(row), pattern), header, ReportAlignment.Center, width);
        }

        public ReportColumns<T> AddYesNo(Func<T, bool> value, string header, float width = 1f)
        {
            return Add(row => value(row) ? "Sí" : "No", header, ReportAlignment.Center, width);
        }

        public ReportColumns<T> AddIf(bool condition, Func<T, string> value, string header, ReportAlignment alignment = ReportAlignment.Left, float width = 1f)
        {
            return condition ? Add(value, header, alignment, width) : this;
        }

        public List<ReportColumn<T>> Build()
        {
            return _columns;
        }

        #region Private
        private static string Format(object value, string format)
        {
            if (value == null)
                return string.Empty;

            return value is IFormattable formattable
                ? formattable.ToString(format, ReportCulture.Current)
                : Convert.ToString(value, ReportCulture.Current);
        }

        private static string FormatDate(object value, string pattern)
        {
            if (value == null)
                return string.Empty;

            return Convert.ToDateTime(value, ReportCulture.Current).ToString(pattern, ReportCulture.Current);
        }
        #endregion
    }

    public static class ReportColumns
    {
        public static ReportColumns<T> For<T>()
        {
            return new ReportColumns<T>();
        }
    }

    internal static class ReportCulture
    {
        // Resolved defensively: containers in globalization-invariant mode reject named cultures
        public static readonly CultureInfo Current = Resolve();

        private static CultureInfo Resolve()
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
    }
}
