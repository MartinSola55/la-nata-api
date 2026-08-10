using System;

namespace Template_Api.Core.Models.Internal
{
    public class S3FileInfo
    {
        public string Key { get; set; }
        public string ContentType { get; set; }
        public long Size { get; set; }
        public DateTime? LastModified { get; set; }
        public string ETag { get; set; }
    }
}
