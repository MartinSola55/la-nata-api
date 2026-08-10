using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using Template_Api.Core.Models.Constants;
using Template_Api.Core.Models.DTO;

namespace Template_Api.Core.Helpers
{
    public class FileHelper
    {
        private static readonly Dictionary<string, string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["application/pdf"] = ".pdf",
            ["image/jpeg"] = ".jpg",
            ["image/png"] = ".png",
        };

        private const long MaxFileSizeBytes = 15 * 1024 * 1024;

        public static bool IsValidFileType(string contentType)
        {
            return AllowedMimeTypes.ContainsKey(NormalizeContentType(contentType));
        }

        public static bool IsValidFileSize(long fileSizeInBytes)
        {
            return fileSizeInBytes <= MaxFileSizeBytes;
        }

        public static BaseResponse<T> ValidateFile<T>(IFormFile file)
        {
            var rs = new BaseResponse<T>();

            if (file == null || file.Length == 0)
                return rs.SetError(Messages.Error.FieldRequired("archivo"));

            if (!IsValidFileType(file.ContentType))
                return rs.SetError(Messages.Error.InvalidFileType());

            if (!IsValidFileSize(file.Length))
                return rs.SetError(Messages.Error.FileTooLarge());

            return rs;
        }

        public static string NormalizeContentType(string contentType)
        {
            if (string.IsNullOrWhiteSpace(contentType))
                return string.Empty;

            var separatorIndex = contentType.IndexOf(';');
            if (separatorIndex >= 0)
                contentType = contentType[..separatorIndex];

            return contentType.Trim().ToLowerInvariant();
        }

        public static string GetExtension(string contentType)
        {
            return AllowedMimeTypes.TryGetValue(NormalizeContentType(contentType), out var extension) ? extension : string.Empty;
        }
    }
}
