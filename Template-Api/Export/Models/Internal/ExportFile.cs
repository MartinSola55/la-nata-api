namespace Template_Api.Export.Models.Internal
{
    public class ExportFile
    {
        public string FileName { get; set; }
        public string ContentType { get; set; }
        public byte[] Content { get; set; }
        public int RowCount { get; set; }
        public long Size => Content?.LongLength ?? 0;
    }
}
