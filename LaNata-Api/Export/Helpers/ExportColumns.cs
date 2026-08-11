using System;
using System.Collections.Generic;
using LaNata_Api.Export.Models.Internal;

namespace LaNata_Api.Export.Helpers
{
    public class ExportColumns<T>
    {
        private readonly List<ExportColumn<T>> _columns = [];

        internal ExportColumns()
        {
        }

        public ExportColumns<T> Add(Func<T, object> value, string header, ExportValueFormat format = ExportValueFormat.None, double? width = null)
        {
            ArgumentNullException.ThrowIfNull(value);

            _columns.Add(new ExportColumn<T>
            {
                Header = header,
                Value = value,
                Format = format,
                Width = width
            });

            return this;
        }

        public ExportColumns<T> Add(string header, Func<T, object> value, ExportValueFormat format = ExportValueFormat.None, double? width = null)
        {
            return Add(value, header, format, width);
        }

        public ExportColumns<T> AddIf(bool condition, Func<T, object> value, string header, ExportValueFormat format = ExportValueFormat.None, double? width = null)
        {
            return condition ? Add(value, header, format, width) : this;
        }

        public List<ExportColumn<T>> Build()
        {
            return _columns;
        }
    }

    public static class ExportColumns
    {
        public static ExportColumns<T> For<T>()
        {
            return new ExportColumns<T>();
        }
    }
}
