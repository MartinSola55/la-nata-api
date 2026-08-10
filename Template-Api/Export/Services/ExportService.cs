using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Template_Api.Core.Models.DTO;
using Template_Api.Export.Helpers;
using Template_Api.Export.Models.Constants;
using Template_Api.Export.Models.Internal;

namespace Template_Api.Export.Services
{
    public class ExportService
    {
        #region Methods
        public BaseResponse<ExportFile> ToCsv<T>(ExportRequest<T> rq)
        {
            var rs = new BaseResponse<ExportFile>();

            var rows = Materialize(rq, rs, out var validation);
            if (!validation)
                return rs;

            try
            {
                var content = CsvWriter.Write(rq.Columns, rows, rq.Delimiter, ResolveCulture(rq));

                rs.Data = BuildFile(rq.FileName, ExportFileExtension.CSV, ExportContentType.CSV, content, rows.Count);
            }
            catch (Exception)
            {
                return rs.SetError(ExportMessages.ExportException());
            }

            return rs;
        }

        public async Task<BaseResponse<ExportFile>> ToExcel<T>(ExportRequest<T> rq)
        {
            var rs = new BaseResponse<ExportFile>();

            var rows = Materialize(rq, rs, out var validation);
            if (!validation)
                return rs;

            try
            {
                var sheetName = string.IsNullOrWhiteSpace(rq.SheetName) ? rq.FileName : rq.SheetName;
                var content = await ExcelWriter.Write(rq.Columns, rows, sheetName, rq.FreezeHeader, ResolveCulture(rq));

                rs.Data = BuildFile(rq.FileName, ExportFileExtension.EXCEL, ExportContentType.EXCEL, content, rows.Count);
            }
            catch (Exception)
            {
                return rs.SetError(ExportMessages.ExportException());
            }

            return rs;
        }

        public async Task<BaseResponse<ExportFile>> Export<T>(ExportFileFormat format, ExportRequest<T> rq)
        {
            return format == ExportFileFormat.Excel
                ? await ToExcel(rq)
                : ToCsv(rq);
        }

        // Multi-sheet: CSV has no equivalent, so this is Excel only
        public async Task<BaseResponse<ExportFile>> ToExcelWorkbook(ExportWorkbookRequest rq)
        {
            var rs = new BaseResponse<ExportFile>();

            if (rq == null || rq.Sheets == null || rq.Sheets.Count == 0)
                return rs.SetError(ExportMessages.SheetsRequired());

            var totalRows = rq.Sheets.Sum(x => x.Rows?.Count ?? 0);
            if (totalRows > ExportLimits.MaxRows)
                return rs.SetError(ExportMessages.TooManyRows(ExportLimits.MaxRows));

            try
            {
                var content = await ExcelWriter.Write(rq.Sheets, ExportValueFormatter.DefaultCulture);

                rs.Data = BuildFile(rq.FileName, ExportFileExtension.EXCEL, ExportContentType.EXCEL, content, totalRows);
            }
            catch (Exception)
            {
                return rs.SetError(ExportMessages.ExportException());
            }

            return rs;
        }
        #endregion

        #region Private
        // Pulls the sequence into a list once, so a lazy query is not re-executed per column
        private static List<T> Materialize<T>(ExportRequest<T> rq, BaseResponse<ExportFile> rs, out bool isValid)
        {
            isValid = false;

            if (rq == null || rq.Columns == null || rq.Columns.Count == 0)
            {
                rs.SetError(ExportMessages.ColumnsRequired());
                return [];
            }

            var rows = rq.Rows == null ? [] : rq.Rows.ToList();

            if (rows.Count > ExportLimits.MaxRows)
            {
                rs.SetError(ExportMessages.TooManyRows(ExportLimits.MaxRows));
                return [];
            }

            isValid = true;
            return rows;
        }

        private static ExportFile BuildFile(string fileName, string extension, string contentType, byte[] content, int rowCount)
        {
            return new ExportFile
            {
                FileName = BuildFileName(fileName, extension),
                ContentType = contentType,
                Content = content,
                RowCount = rowCount
            };
        }

        // Timestamped so repeated downloads do not collide in the browser
        private static string BuildFileName(string fileName, string extension)
        {
            var name = string.IsNullOrWhiteSpace(fileName) ? "export" : fileName.Trim();

            if (name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                name = name[..^extension.Length];

            foreach (var invalid in System.IO.Path.GetInvalidFileNameChars())
                name = name.Replace(invalid, '_');

            return name + "_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmm", CultureInfo.InvariantCulture) + extension;
        }

        private static CultureInfo ResolveCulture<T>(ExportRequest<T> rq)
        {
            return ExportValueFormatter.DefaultCulture;
        }
        #endregion
    }
}
