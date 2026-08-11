using System;
using System.IO;

namespace LaNata_Api.Report.Helpers
{
    public static class ReportAssets
    {
        public static byte[] Read(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return [];

            var path = Path.Combine(AppContext.BaseDirectory, "Report", "Assets", fileName);
            return File.Exists(path) ? File.ReadAllBytes(path) : [];
        }
    }
}
