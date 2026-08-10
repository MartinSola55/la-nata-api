using Microsoft.Extensions.Configuration;
using QuestPDF.Infrastructure;
using System;

namespace Template_Api.Report.Config
{
    public class ReportConfig
    {
        // Must run before the first render or QuestPDF throws
        public static void ConfigureReporting(IConfiguration config)
        {
            var license = config["Reporting:License"];

            QuestPDF.Settings.License = Enum.TryParse<LicenseType>(license, ignoreCase: true, out var licenseType)
                ? licenseType
                : LicenseType.Community;

            // Keeps a layout mistake visible as a red block in the PDF instead of throwing and failing the whole request
            QuestPDF.Settings.EnableDebugging = config.GetValue("Reporting:EnableDebugging", false);
        }
    }
}
