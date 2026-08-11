using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using LaNata_Api.Core.Models.DTO;
using LaNata_Api.Report.Helpers.Documents;
using LaNata_Api.Report.Models.Constants;
using LaNata_Api.Report.Models.Internal;

namespace LaNata_Api.Report.Services
{
    public class ReportService
    {
        private const string PDF_CONTENT_TYPE = "application/pdf";
        private const string PDF_EXTENSION = ".pdf";

        #region Methods
        public BaseResponse<ReportFile> GenerateTable<T>(ReportTableRequest<T> rq)
        {
            var rs = new BaseResponse<ReportFile>();

            if (rq == null || rq.Columns == null || rq.Columns.Count == 0)
                return rs.SetError(ReportMessages.ColumnsRequired());

            var rows = rq.Rows == null ? [] : rq.Rows.ToList();

            if (rows.Count > ReportLimits.MaxRows)
                return rs.SetError(ReportMessages.TooManyRows(ReportLimits.MaxRows));

            return Generate(new TableReportDocument<T>(rq, rows), rq.FileName);
        }

        public BaseResponse<ReportFile> Generate(IDocument document, string fileName)
        {
            var rs = new BaseResponse<ReportFile>();

            if (document == null)
                return rs.SetError(ReportMessages.DocumentRequired());

            try
            {
                using var stream = new MemoryStream();
                document.GeneratePdf(stream);

                rs.Data = new ReportFile
                {
                    FileName = BuildFileName(fileName),
                    ContentType = PDF_CONTENT_TYPE,
                    Content = stream.ToArray()
                };
            }
            catch (Exception)
            {
                return rs.SetError(ReportMessages.ReportException());
            }

            return rs;
        }

        // Renders several documents into one PDF, each keeping its own page numbering
        public BaseResponse<ReportFile> Merge(List<IDocument> documents, string fileName)
        {
            var rs = new BaseResponse<ReportFile>();

            if (documents == null || documents.Count == 0)
                return rs.SetError(ReportMessages.DocumentRequired());

            try
            {
                using var stream = new MemoryStream();

                Document
                    .Merge(documents)
                    .UseOriginalPageNumbers()
                    .GeneratePdf(stream);

                rs.Data = new ReportFile
                {
                    FileName = BuildFileName(fileName),
                    ContentType = PDF_CONTENT_TYPE,
                    Content = stream.ToArray()
                };
            }
            catch (Exception)
            {
                return rs.SetError(ReportMessages.ReportException());
            }

            return rs;
        }
        #endregion

        #region Private
        private static string BuildFileName(string fileName)
        {
            var name = string.IsNullOrWhiteSpace(fileName) ? "reporte" : fileName.Trim();

            if (name.EndsWith(PDF_EXTENSION, StringComparison.OrdinalIgnoreCase))
                name = name[..^PDF_EXTENSION.Length];

            foreach (var invalid in Path.GetInvalidFileNameChars())
                name = name.Replace(invalid, '_');

            return name + "_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmm", CultureInfo.InvariantCulture) + PDF_EXTENSION;
        }
        #endregion
    }
}
