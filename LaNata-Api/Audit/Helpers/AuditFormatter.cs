using System;
using System.Globalization;
using LaNata_Api.Audit.Models.Constants;
using LaNata_Api.Audit.Models.Internal;

namespace LaNata_Api.Audit.Helpers
{
    // Invariant culture on purpose: this text is stored and compared, so it must not depend on
    // the culture of the machine running the API
    internal static class AuditFormatter
    {
        private const string DATE_FORMAT = "dd/MM/yyyy";
        private const string DATE_TIME_FORMAT = "dd/MM/yyyy HH:mm";
        private const string DECIMAL_FORMAT = "N2";

        public static string Describe(object value, AuditFormat format)
        {
            var text = Format(value, format);

            return string.IsNullOrWhiteSpace(text) ? AuditMessages.EmptyValue() : text;
        }

        public static string Format(object value, AuditFormat format)
        {
            if (value == null)
                return null;

            var culture = CultureInfo.InvariantCulture;

            return format switch
            {
                AuditFormat.Date => Convert.ToDateTime(value, culture).ToString(DATE_FORMAT, culture),
                AuditFormat.DateTime => Convert.ToDateTime(value, culture).ToString(DATE_TIME_FORMAT, culture),
                AuditFormat.Decimal => Convert.ToDecimal(value, culture).ToString(DECIMAL_FORMAT, culture),
                AuditFormat.Currency => "$" + Convert.ToDecimal(value, culture).ToString(DECIMAL_FORMAT, culture),
                AuditFormat.Percentage => Convert.ToDecimal(value, culture).ToString(DECIMAL_FORMAT, culture) + "%",
                AuditFormat.YesNo => Convert.ToBoolean(value, culture) ? "Sí" : "No",
                AuditFormat.ActiveInactive => Convert.ToBoolean(value, culture) ? "Activo" : "Inactivo",
                _ => Convert.ToString(value, culture)
            };
        }
    }
}
