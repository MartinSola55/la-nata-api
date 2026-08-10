namespace Template_Api.Core.Models.Internal
{
    public class S3File
    {
        public string Key { get; set; }
        public string FileName { get; set; }
        public string ContentType { get; set; }
        public byte[] Content { get; set; }
        public long Size => Content?.LongLength ?? 0;
    }
}
