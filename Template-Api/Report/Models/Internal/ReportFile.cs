namespace Template_Api.Report.Models.Internal
{
    public class ReportFile
    {
        public string FileName { get; set; }
        public string ContentType { get; set; }
        public byte[] Content { get; set; }
        public long Size => Content?.LongLength ?? 0;
    }
}
