using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Template_Api.Core.Helpers;
using Template_Api.Core.Models.Constants;
using Template_Api.Core.Models.DTO;
using Template_Api.Core.Models.Internal;
using Template_Api.Core.Security.Config;

namespace Template_Api.Core.Services
{
    public class AwsS3Service(IAmazonS3 amazonS3, IOptions<AwsConfig.AwsOptions> opts)
    {
        private const int MaxKeysPerDeleteRequest = 1000;
        private const int MaxKeyLength = 200;
        private const int MaxIdLength = 64;
        private const int DefaultSignedUrlMinutes = 15;
        private const int MaxSignedUrlMinutes = 60;
        private const string FallbackDownloadName = "descarga";

        private readonly IAmazonS3 _amazonS3 = amazonS3;
        private readonly AwsConfig.AwsOptions _opts = opts.Value;

        #region Methods
        public async Task<BaseResponse<string>> UploadFileAsync(IFormFile file, string folder, string id = null, CancellationToken ct = default)
        {
            var rs = new BaseResponse<string> { Data = string.Empty };

            if (!FolderNames.IsValid(folder))
                return rs.SetError(Messages.Error.InvalidField("carpeta"));

            var validation = FileHelper.ValidateFile<string>(file);
            if (!validation.Success)
                return rs.Attach(validation);

            if (!IsEnabled())
                return rs;

            var key = BuildFileName(id, file.ContentType);

            try
            {
                using var stream = file.OpenReadStream();

                var request = new PutObjectRequest
                {
                    BucketName = _opts.BucketName,
                    Key = BuildKey(key, folder),
                    InputStream = stream,
                    AutoCloseStream = false,
                    ContentType = FileHelper.NormalizeContentType(file.ContentType),
                    ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256
                };

                await _amazonS3.PutObjectAsync(request, ct);
            }
            catch (Exception)
            {
                return rs.SetError(Messages.Error.AwsS3Exception());
            }

            rs.Data = key;
            return rs;
        }

        public async Task<BaseResponse<List<string>>> UploadFileListAsync(List<IFormFile> files, string folder, string id = null, CancellationToken ct = default)
        {
            var rs = new BaseResponse<List<string>> { Data = [] };

            if (files == null || files.Count == 0)
                return rs.SetError(Messages.Error.FieldRequired("archivos"));

            foreach (var file in files)
            {
                var upload = await UploadFileAsync(file, folder, id, ct);

                if (upload.Success)
                {
                    if (!string.IsNullOrEmpty(upload.Data))
                        rs.Data.Add(upload.Data);

                    continue;
                }

                var uploaded = rs.Data;
                rs.Data = [];
                await DeleteFileListAsync(uploaded, folder, ct);

                return rs.SetError(upload.Error.Message);
            }

            return rs;
        }

        public async Task<BaseResponse<bool>> FileExistsAsync(string key, string folder, CancellationToken ct = default)
        {
            var rs = new BaseResponse<bool>();

            var info = await GetFileInfoAsync(key, folder, ct);
            if (!info.Success)
                return rs.SetError(info.Error.Message);

            rs.Data = info.Data != null;
            return rs;
        }

        public async Task<BaseResponse<S3FileInfo>> GetFileInfoAsync(string key, string folder, CancellationToken ct = default)
        {
            var rs = new BaseResponse<S3FileInfo>();

            var error = ValidateLocation(key, folder);
            if (error != null)
                return rs.SetError(error);

            if (!IsEnabled())
                return rs;

            var request = new GetObjectMetadataRequest
            {
                BucketName = _opts.BucketName,
                Key = BuildKey(key, folder)
            };

            try
            {
                var response = await _amazonS3.GetObjectMetadataAsync(request, ct);

                rs.Data = new S3FileInfo
                {
                    Key = key,
                    ContentType = response.Headers.ContentType,
                    Size = response.Headers.ContentLength,
                    LastModified = response.LastModified,
                    ETag = response.ETag
                };

                return rs;
            }
            // A bucket without s3:ListBucket answers 403 instead of 404 for missing keys.
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound || ex.StatusCode == HttpStatusCode.Forbidden)
            {
                return rs;
            }
            catch (Exception)
            {
                return rs.SetError(Messages.Error.AwsS3Exception());
            }
        }

        public async Task<BaseResponse<S3File>> DownloadFileAsync(string key, string folder, string downloadName = null, CancellationToken ct = default)
        {
            var rs = new BaseResponse<S3File>();

            var error = ValidateLocation(key, folder);
            if (error != null)
                return rs.SetError(error);

            if (!IsEnabled())
                return rs.SetError(Messages.Error.AwsS3NotConfigured());

            var request = new GetObjectRequest
            {
                BucketName = _opts.BucketName,
                Key = BuildKey(key, folder)
            };

            try
            {
                using var response = await _amazonS3.GetObjectAsync(request, ct);

                // Checked before reading the body so an oversized object is never buffered.
                if (!FileHelper.IsValidFileSize(response.Headers.ContentLength))
                    return rs.SetError(Messages.Error.FileTooLarge());

                using var buffer = new MemoryStream();
                await response.ResponseStream.CopyToAsync(buffer, ct);

                rs.Data = new S3File
                {
                    Key = key,
                    FileName = SanitizeDownloadName(downloadName ?? key),
                    ContentType = response.Headers.ContentType,
                    Content = buffer.ToArray()
                };

                return rs;
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound || ex.StatusCode == HttpStatusCode.Forbidden)
            {
                return rs.SetError(Messages.Error.FileNotFound());
            }
            catch (Exception)
            {
                return rs.SetError(Messages.Error.AwsS3Exception());
            }
        }

        public async Task<BaseResponse<string>> GetSignedUrlByFileKeyAsync(string key, string folder, string downloadName = null, CancellationToken ct = default)
        {
            var rs = new BaseResponse<string>();

            var error = ValidateLocation(key, folder);
            if (error != null)
                return rs.SetError(error);

            if (!IsEnabled())
                return rs.SetError(Messages.Error.AwsS3NotConfigured());

            var exists = await FileExistsAsync(key, folder, ct);
            if (!exists.Success)
                return rs.SetError(exists.Error.Message);

            if (!exists.Data)
                return rs.SetError(Messages.Error.FileNotFound());

            var request = new GetPreSignedUrlRequest
            {
                BucketName = _opts.BucketName,
                Key = BuildKey(key, folder),
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.AddMinutes(GetSignedUrlMinutes()),
                ResponseHeaderOverrides = new ResponseHeaderOverrides
                {
                    ContentDisposition = BuildContentDisposition(downloadName ?? key)
                }
            };

            try
            {
                rs.Data = await _amazonS3.GetPreSignedURLAsync(request);
                return rs;
            }
            catch (Exception)
            {
                return rs.SetError(Messages.Error.AwsS3Exception());
            }
        }

        public async Task<BaseResponse> DeleteFileAsync(string key, string folder, CancellationToken ct = default)
        {
            var rs = new BaseResponse();

            var error = ValidateLocation(key, folder);
            if (error != null)
                return rs.SetError(error);

            if (!IsEnabled())
                return rs;

            try
            {
                await _amazonS3.DeleteObjectAsync(_opts.BucketName, BuildKey(key, folder), ct);
                return rs;
            }
            catch (Exception)
            {
                return rs.SetError(Messages.Error.AwsS3Exception());
            }
        }

        public async Task<BaseResponse> DeleteFileListAsync(List<string> keys, string folder, CancellationToken ct = default)
        {
            var rs = new BaseResponse();

            if (keys == null || keys.Count == 0)
                return rs;

            foreach (var key in keys)
            {
                var error = ValidateLocation(key, folder);
                if (error != null)
                    return rs.SetError(error);
            }

            if (!IsEnabled())
                return rs;

            try
            {
                foreach (var batch in keys.Chunk(MaxKeysPerDeleteRequest))
                {
                    var request = new DeleteObjectsRequest
                    {
                        BucketName = _opts.BucketName,
                        Objects = [.. batch.Select(key => new KeyVersion { Key = BuildKey(key, folder) })]
                    };

                    var response = await _amazonS3.DeleteObjectsAsync(request, ct);

                    if (response.DeleteErrors != null && response.DeleteErrors.Count > 0)
                        return rs.SetError(Messages.Error.AwsS3Exception());
                }

                return rs;
            }
            catch (Exception)
            {
                return rs.SetError(Messages.Error.AwsS3Exception());
            }
        }
        #endregion

        #region Validations
        private static string ValidateLocation(string key, string folder)
        {
            if (!FolderNames.IsValid(folder))
                return Messages.Error.InvalidField("carpeta");

            if (!IsValidKey(key))
                return Messages.Error.InvalidFileKey();

            return null;
        }

        private static bool IsValidKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || key.Length > MaxKeyLength)
                return false;

            if (key.Contains("..") || key.Contains('/') || key.Contains('\\') || key.Contains(':'))
                return false;

            return !key.Any(char.IsControl);
        }
        #endregion

        #region Private
        private bool IsEnabled()
        {
            return _opts.Enabled && !string.IsNullOrWhiteSpace(_opts.BucketName);
        }

        private int GetSignedUrlMinutes()
        {
            if (_opts.SignedUrlMinutes <= 0)
                return DefaultSignedUrlMinutes;

            return Math.Min(_opts.SignedUrlMinutes, MaxSignedUrlMinutes);
        }

        private static string BuildKey(string key, string folder)
        {
            return $"{folder}/{key}";
        }

        private static string BuildFileName(string id, string contentType)
        {
            var extension = FileHelper.GetExtension(contentType);
            var sanitizedId = SanitizeId(id);
            var key = string.IsNullOrEmpty(sanitizedId) ? Guid.NewGuid().ToString() : $"{sanitizedId}_{Guid.NewGuid()}";

            return $"{DateTime.UtcNow:yyyy-MM-dd}_{key}{extension}";
        }

        private static string SanitizeId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return string.Empty;

            var sanitized = new string([.. id.Where(c => char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_')]);

            return sanitized.Length > MaxIdLength ? sanitized[..MaxIdLength] : sanitized;
        }

        private static string SanitizeDownloadName(string downloadName)
        {
            var name = Path.GetFileName(downloadName ?? string.Empty);
            name = new string([.. name.Where(c => !char.IsControl(c) && c != '"' && c != '\\' && c != '/')]);

            return string.IsNullOrWhiteSpace(name) ? FallbackDownloadName : name;
        }

        private static string BuildContentDisposition(string downloadName)
        {
            var name = SanitizeDownloadName(downloadName);
            var fallback = new string([.. name.Where(char.IsAscii)]);

            if (string.IsNullOrWhiteSpace(fallback))
                fallback = FallbackDownloadName;

            return $"attachment; filename=\"{fallback}\"; filename*=UTF-8''{Uri.EscapeDataString(name)}";
        }
        #endregion
    }
}
